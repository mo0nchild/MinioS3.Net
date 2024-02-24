
using Microsoft.OpenApi.Models;
using Minio;
using static System.Net.Mime.MediaTypeNames;

namespace ModelsApp.S3
{
    public class Program
    {
        public static readonly string S3Endpoint = "host.docker.internal:9000";
        //public static readonly string S3Endpoint = "172.24.0.2:9000";
        //public static readonly string S3Endpoint = "minio:9000";
        public static readonly string S3AccessKey = "apiclient";
        public static readonly string S3SecretKey = "1234567890";

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddMinio(options =>
            {
                options.WithEndpoint(S3Endpoint);
                options.WithCredentials(S3AccessKey, S3SecretKey);
                options.WithSSL(false);
            });
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("s3api", new OpenApiInfo()
                {
                    Title = "API for S3",
                    Version = "v1",
                    Description = "Web-Api for testing Minio object storage",
                    Contact = new OpenApiContact()
                    {
                        Name = "byterbrod",
                        Url = new Uri("https://github.com/mo0nchild")
                    }
                });
                var localpath = AppDomain.CurrentDomain.BaseDirectory;
                options.IncludeXmlComments(Path.Combine(localpath, "ModelsApp.S3.xml"));
            });

            var application = builder.Build();
            if (application.Environment.IsDevelopment())
            {
                application.UseSwagger();
                application.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/swagger/s3api/swagger.json", "S3 Web API");
                });
            }
            application.UseHttpsRedirection();
            application.MapControllers();
            application.Run();
        }
    }
}
