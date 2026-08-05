using System.ComponentModel.DataAnnotations;

namespace Mango.Services.CouponAPI.Models.Dto
{
    public class CouponDto
    {
        public int CouponId { get; set; }
        [Required]
        public string CouponCode { get; set; }
        [Range(0.01, double.MaxValue, ErrorMessage = "DiscountAmount must be greater than 0")]
        public double DiscountAmount { get; set; }
        [Range(0, int.MaxValue, ErrorMessage = "MinAmount must be greater than or equal to 0")]
        public int MinAmount { get; set; }
    }
}
