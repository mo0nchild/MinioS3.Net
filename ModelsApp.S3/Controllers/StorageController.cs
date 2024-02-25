using Microsoft.AspNetCore.Mvc;
using Minio;
using Minio.DataModel.Args;
using System.IO;
using System;
using System.Net.Mime;
using System.Security.AccessControl;
using CommunityToolkit.HighPerformance.Buffers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MinioS3Test.Controllers
{
    /// <summary>
    /// Контроллер для работы с объектным хранилищем
    /// </summary>
    [Route("storage"), ApiController]
    public class StorageController : ControllerBase
    {
        private readonly IMinioClientFactory minioFactory = default!;
        private readonly ILogger<StorageController> logger = default!;

        public StorageController(IMinioClientFactory minioFactory, ILogger<StorageController> logger) : base()
        {
            this.minioFactory = minioFactory;
            this.logger = logger;
        }
        [Route("healthcheck"), HttpGet]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        public async Task<IActionResult> HealthCheck() 
        {
            return new OkObjectResult(new { status = "all good" });
        }
        [Route("checkPresignedUrl"), HttpGet]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        public async Task<IActionResult> CheckPresignedUrl(string url, [FromServices] IHttpClientFactory factory)
        {
            using (var client = factory.CreateClient("ApiClient"))
            {
                var result = await client.SendAsync(new HttpRequestMessage()
                {
                    RequestUri = new Uri(url),
                    Method = HttpMethod.Get,
                });
                if (!result.IsSuccessStatusCode) return this.BadRequest("Не удалось обработать");
                this.logger.LogInformation((await result.Content.ReadAsByteArrayAsync()).Length.ToString());
            }
            return this.Ok("Данные были получены");
        }
        [Route("getBuckets"), HttpGet] 
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetBuckets()
        {
            using (var minioClient = this.minioFactory.CreateClient())
            {
                var getListBucketsTask = await minioClient.ListBucketsAsync();
                if (getListBucketsTask == null) return this.BadRequest("Корзины не найдены");

                foreach (var bucket in getListBucketsTask.Buckets)
                {
                    if (bucket == null) continue;
                    this.logger.LogInformation(bucket.Name + " " + bucket.CreationDateDateTime);
                }
                return this.Ok("Корзины найдены");
            }
        }
        [Route("uploadFile"), HttpPost]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        public async Task<IActionResult> UploadFile(IFormFile formFile, string bucketName)
        {
            using (var minioClient = this.minioFactory.CreateClient())
            {
                var beArgs = new BucketExistsArgs().WithBucket(bucketName);
                if(!await minioClient.BucketExistsAsync(beArgs))
                {
                    return this.BadRequest("Бакет не был найден");
                }
                this.logger.LogInformation("Бакет найден, начинаем загрузку");
                using (var filestream = formFile.OpenReadStream())
                {
                    using var requestData = new MemoryStream();
                    filestream.CopyTo(requestData);
                    requestData.Seek(0, SeekOrigin.Begin);

                    this.logger.LogInformation($"{formFile.Length} <-> {requestData.Length}");
                    var putObjectArgs = new PutObjectArgs()
                        .WithBucket(bucketName)
                        .WithStreamData(requestData)
                        .WithContentType("application/octet-stream")
                        .WithObject(formFile.FileName)
                        .WithObjectSize(formFile.Length);

                    try { await minioClient.PutObjectAsync(putObjectArgs); }
                    catch (Exception errorInfo) { return this.BadRequest($"Ошибка добавления: {errorInfo.Message}"); }
                }
                return this.Ok("Файл был загружен");
            }
        }
        [Route("getUrl"), HttpPost]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetFileUrl(/*string fileName, string bucketName*/)
        {
            var fileName = "myimage.png";
            var bucketName = "testbucket";
            using (var minioClient = this.minioFactory.CreateClient())
            {
                var presignedGetArgs = new PresignedGetObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(fileName)
                    .WithExpiry((int)new TimeSpan(24, 0, 0).TotalSeconds);

                this.logger.LogInformation($"Выдаем {fileName} на время: {new TimeSpan(24, 0, 0).TotalSeconds}");
                var objectUrl = await minioClient.PresignedGetObjectAsync(presignedGetArgs);
         
                if (objectUrl == null) return this.BadRequest("Файл не найден");
                return this.Ok(objectUrl);
            }
        }
    }
}
