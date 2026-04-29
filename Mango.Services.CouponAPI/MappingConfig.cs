using AutoMapper;
using Mango.Services.CouponAPI.Models;
using Mango.Services.CouponAPI.Models.Dto;
using Microsoft.Extensions.Logging.Abstractions;

namespace Mango.Services.CouponAPI
{
    public class MappingConfig
    {
        public static MapperConfiguration RegisterMaps()
        {
            var nullLoggerFactory = new NullLoggerFactory();

            var mappingConfig = new MapperConfiguration(config => 
            {
                config.CreateMap<CouponDto, Coupon>();
                config.CreateMap<Coupon, CouponDto>();
            }, nullLoggerFactory);
            return mappingConfig;
        }
    }
}
