using AutoMapper;
using Mango.Services.ProductAPI.Models;
using Mango.Services.ProductAPI.Models.Dto;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mango.Services.ProductAPI
{
    public class MappingConfig
    {
        public static MapperConfiguration RegisterMaps()
        {
            var nullLoggerFactory = new NullLoggerFactory();

            var mappingConfig = new MapperConfiguration(config =>
            {
                config.CreateMap<ProductDto, Product>().ReverseMap();
            }, nullLoggerFactory);
            return mappingConfig;
        }
    }
}