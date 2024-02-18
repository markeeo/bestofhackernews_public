using Serilog;
using AutoMapper;
using BestOfHackerNews.Dto;

namespace BestOfHackerNews
{
    public class Program
    {
        public static void Main(string[] args)
        {
            
            var builder = WebApplication.CreateBuilder(args);
            
            builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration));
            
            ConfigurationManager configuration = builder.Configuration;
            AppSettings appSettings = new AppSettings();
            configuration.GetSection("AppSettings").Bind(appSettings);
           
            // Add services to the container.

            builder.Services.AddControllers();
            
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            
            builder.Services.AddSingleton(appSettings);
            builder.Services.AddSingleton<IStoryManager, StoryManager>();

            IMapper typeMapper = SetupTypeMapper();
            builder.Services.AddSingleton(typeMapper);

            var app = builder.Build();

            //Startup any services
            StartPrequisiteServices(app.Services, appSettings);
            

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }

        public static IMapper SetupTypeMapper()
        {
            MapperConfiguration mapperConfig = new MapperConfiguration(PrepareConversionMappings);
            return mapperConfig.CreateMapper();
        }

        public static void PrepareConversionMappings(IMapperConfigurationExpression cfg)
        {
            cfg.CreateMap<HackerNewsStoryDto, StoryDto>()
                    .ForMember(story => story.commentCount, opt => opt.MapFrom(hkNews => hkNews.descendants))
                    .ForMember(story => story.time, opt => opt.MapFrom(hkNews => DateTimeOffset.FromUnixTimeSeconds(hkNews.time).ToString("yyyy-MM-ddTHH:mm:ssK")))
                    .ForMember(story => story.uri, opt => opt.MapFrom(hkNews => hkNews.url));

        }
        public static  void StartPrequisiteServices(IServiceProvider serviceProvider, AppSettings appSettings)
        {
            ILogger<Program> log = serviceProvider.GetService<ILogger<Program>>();

            //Start up the story loading routine
            log.LogInformation("Starting Story Manager");
            IStoryManager sMgr = serviceProvider.GetRequiredService<IStoryManager>();
            Task t= Task.Run(async () =>await sMgr.Start());

            //Allow time for startup to complete
            if (!t.Wait((int)appSettings.StartupTimeout.TotalMilliseconds))
            {
                log.LogError($"Failed to start Story Manager withing the required {appSettings.StartupTimeout}. Terminating process");
                Environment.Exit(-1);
            }
            log.LogInformation("Story Mananager Ready");

        }
    }
}
