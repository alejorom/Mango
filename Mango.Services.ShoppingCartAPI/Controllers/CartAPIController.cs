using AutoMapper;
using Mango.MessageBus.Service.IService;
using Mango.Services.ShoppingCartAPI.Data;
using Mango.Services.ShoppingCartAPI.Models;
using Mango.Services.ShoppingCartAPI.Models.Dto;
using Mango.Services.ShoppingCartAPI.Service.IService;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Data;
using System.Net.Mail;

namespace Mango.Services.ShoppingCartAPI.Controllers
{
    [Route("api/cart")]
    [ApiController]
    [Authorize]
    public class CartAPIController(AppDbContext db, IMapper mapper, IProductService productService, 
        ICouponService couponService, IMessageBus messageBus, IConfiguration configuration) : ControllerBase
    {
        private ResponseDto _response = new ResponseDto();
        private IMapper _mapper = mapper;
        private readonly AppDbContext _db = db;
        private IProductService _productService = productService;
        private ICouponService _couponService = couponService;
        private IMessageBus _messageBus = messageBus;
        private IConfiguration _configuration = configuration;

        [HttpGet("GetCart/{userId}")]
        public async Task<ActionResult<ResponseDto>> GetCart(string userId)
        {
            if (!Guid.TryParseExact(userId, "D", out _))
            {
                return BadRequest(new ResponseDto { IsSuccess = false, Message = "Invalid user identifier" });
            }

            var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (!string.Equals(userId, subject, StringComparison.Ordinal))
            {
                return NotFound(new ResponseDto { IsSuccess = false, Message = "Cart not found" });
            }

            try
            {
                var cartHeader = await _db.CartHeaders.FirstOrDefaultAsync(u => u.UserId == subject);
                if (cartHeader is null)
                {
                    return NotFound(new ResponseDto { IsSuccess = false, Message = "Cart not found" });
                }

                CartDto cart = new()
                {
                    CartHeader = _mapper.Map<CartHeaderDto>(cartHeader)
                };
                cart.CartDetails = _mapper.Map<IEnumerable<CartDetailsDto>>(_db.CartDetails
                    .Where(u => u.CartHeaderId == cart.CartHeader.CartHeaderId));

                IEnumerable<ProductDto> productDtos = await _productService.GetProducts();

                foreach (var item in cart.CartDetails)
                {
                    item.Product = productDtos.FirstOrDefault(u => u.ProductId == item.ProductId);
                    if (item.Product != null)
                    {
                        cart.CartHeader.CartTotal += (item.Count * item.Product.Price);
                    }
                }

                var unresolvedIds = cart.CartDetails
                    .Where(u => u.Product == null)
                    .Select(u => u.ProductId)
                    .ToList();

                if (unresolvedIds.Any())
                {
                    _response.IsSuccess = false;
                    _response.Message = $"Products not found in catalog: {string.Join(", ", unresolvedIds)}";
                }

                if (!unresolvedIds.Any() && !string.IsNullOrEmpty(cart.CartHeader.CouponCode))
                {
                    CouponDto? coupon = await _couponService.GetCoupon(cart.CartHeader.CouponCode);
                    if (coupon != null && cart.CartHeader.CartTotal > coupon.MinAmount)
                    {
                        cart.CartHeader.CartTotal -= coupon.DiscountAmount;
                        cart.CartHeader.Discount = coupon.DiscountAmount;
                    }
                }

                _response.Result = cart;
            }
            catch (Exception ex)
            {
                _response.IsSuccess = false;
                _response.Message = ex.Message;
                return StatusCode(500, _response);
            }
            return _response;
        }

        [HttpPost("ApplyCoupon")]
        public async Task<ActionResult<ResponseDto>> ApplyCoupon([FromBody] CartDto cartDto)
        {
            var userId = cartDto?.CartHeader?.UserId;
            if (!Guid.TryParseExact(userId, "D", out _))
            {
                return BadRequest(new ResponseDto { IsSuccess = false, Message = "Invalid user identifier" });
            }

            var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (!string.Equals(userId, subject, StringComparison.Ordinal))
            {
                return NotFound(new ResponseDto { IsSuccess = false, Message = "Cart not found" });
            }

            try
            {
                var cartId = await _db.CartHeaders.Where(u => u.UserId == subject)
                    .Select(u => (int?)u.CartHeaderId).FirstOrDefaultAsync();
                if (cartId is null)
                {
                    return NotFound(new ResponseDto { IsSuccess = false, Message = "Cart not found" });
                }

                var updated = await _db.CartHeaders.Where(u => u.CartHeaderId == cartId && u.UserId == subject)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.CouponCode, cartDto.CartHeader.CouponCode));
                if (updated != 1)
                {
                    return NotFound(new ResponseDto { IsSuccess = false, Message = "Cart not found" });
                }
                _response.Result = true;
            }
            catch (Exception)
            {
                _response.IsSuccess = false;
                _response.Message = "An unexpected error occurred";
                return StatusCode(500, _response);
            }
            return _response;
        }

        [HttpPost("CartUpsert")]
        public async Task<ActionResult<ResponseDto>> CartUpsert(CartDto cartDto)
        {
            var userId = cartDto?.CartHeader?.UserId;
            if (!Guid.TryParseExact(userId, "D", out _))
            {
                return BadRequest(new ResponseDto { IsSuccess = false, Message = "Invalid user identifier" });
            }

            var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (!string.Equals(userId, subject, StringComparison.Ordinal))
            {
                return NotFound(new ResponseDto { IsSuccess = false, Message = "Cart not found" });
            }

            var detail = cartDto?.CartDetails?.FirstOrDefault();
            if (detail is null)
            {
                return BadRequest(new ResponseDto { IsSuccess = false, Message = "Cart item is required" });
            }

            try
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                var cartHeaderFromDb = await _db.CartHeaders
                    .FirstOrDefaultAsync(u => u.UserId == subject);
                var details = cartDto!.CartDetails!.ToList();
                if (cartDto.CartHeader.CartHeaderId < 0 || details.Any(item => item is null ||
                    item.CartHeaderId < 0 || item.CartDetailsId < 0 || item.ProductId < 0 ||
                    item.CartHeader?.CartHeaderId < 0 || item.Product?.ProductId < 0 ||
                    (item.CartHeader?.UserId is not null && !Guid.TryParseExact(item.CartHeader.UserId, "D", out _))))
                {
                    return BadRequest(new ResponseDto { IsSuccess = false, Message = "Invalid resource identifier" });
                }

                var ownedHeaderId = cartHeaderFromDb?.CartHeaderId;
                if ((cartDto.CartHeader.CartHeaderId != 0 && cartDto.CartHeader.CartHeaderId != ownedHeaderId) ||
                    details.Any(item => (item.CartHeaderId != 0 && item.CartHeaderId != ownedHeaderId) ||
                        (item.CartHeader?.CartHeaderId is > 0 && item.CartHeader.CartHeaderId != ownedHeaderId) ||
                        (item.CartHeader?.UserId is not null && !string.Equals(item.CartHeader.UserId, subject, StringComparison.Ordinal)) ||
                        (item.Product is not null && item.Product.ProductId != 0 && item.Product.ProductId != item.ProductId)))
                {
                    return NotFound(new ResponseDto { IsSuccess = false, Message = "Cart not found" });
                }

                foreach (var item in details.Where(item => item.CartDetailsId != 0))
                {
                    var matches = await _db.CartDetails.AnyAsync(row => row.CartDetailsId == item.CartDetailsId &&
                        row.CartHeaderId == ownedHeaderId && row.ProductId == item.ProductId);
                    if (!matches)
                    {
                        return NotFound(new ResponseDto { IsSuccess = false, Message = "Cart not found" });
                    }
                }

                if (cartHeaderFromDb == null)
                {
                    CartHeader cartHeader = new() { UserId = subject, CouponCode = cartDto!.CartHeader.CouponCode };
                    _db.CartHeaders.Add(cartHeader);
                    var newDetail = new CartDetails { CartHeader = cartHeader, ProductId = detail.ProductId, Count = detail.Count };
                    _db.CartDetails.Add(newDetail);
                    await _db.SaveChangesAsync();
                    await transaction.CommitAsync();
                    detail.CartHeaderId = cartHeader.CartHeaderId;
                }
                else
                {
                    var cartDetailsFromDb = await _db.CartDetails.FirstOrDefaultAsync(
                        u => u.ProductId == detail.ProductId &&
                        u.CartHeaderId == cartHeaderFromDb.CartHeaderId);
                    if (cartDetailsFromDb == null)
                    {
                        _db.CartDetails.Add(new CartDetails { CartHeaderId = cartHeaderFromDb.CartHeaderId, ProductId = detail.ProductId, Count = detail.Count });
                        await _db.SaveChangesAsync();
                    }
                    else
                    {
                        var count = cartDetailsFromDb.Count + detail.Count;
                        var updated = await _db.CartDetails.Where(row => row.CartDetailsId == cartDetailsFromDb.CartDetailsId &&
                            row.CartHeaderId == cartHeaderFromDb.CartHeaderId && row.CartHeader.UserId == subject)
                            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Count, count));
                        if (updated != 1)
                        {
                            return NotFound(new ResponseDto { IsSuccess = false, Message = "Cart not found" });
                        }
                        detail.Count = count;
                        detail.CartDetailsId = cartDetailsFromDb.CartDetailsId;
                    }
                    await transaction.CommitAsync();
                    detail.CartHeaderId = cartHeaderFromDb.CartHeaderId;
                }
                _response.Result = cartDto;
            }
            catch (Exception)
            {
                _response.Message = "An unexpected error occurred";
                _response.IsSuccess = false;
                return StatusCode(500, _response);
            }
            return _response;
        }

        [HttpPost("RemoveCart")]
        public async Task<ActionResult<ResponseDto>> RemoveCart([FromBody] int cartDetailsId)
        {
            if (cartDetailsId <= 0)
            {
                return BadRequest(new ResponseDto { IsSuccess = false, Message = "Invalid cart item identifier" });
            }

            var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            try
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                CartDetails? cartDetails = await _db.CartDetails.Include(u => u.CartHeader)
                    .FirstOrDefaultAsync(u => u.CartDetailsId == cartDetailsId && u.CartHeader.UserId == subject);

                if (cartDetails == null)
                {
                    _response.IsSuccess = false;
                    _response.Message = "Cart item not found";
                    return NotFound(_response);
                }

                var deleted = await _db.CartDetails.Where(u => u.CartDetailsId == cartDetailsId &&
                    u.CartHeaderId == cartDetails.CartHeaderId && u.CartHeader.UserId == subject).ExecuteDeleteAsync();
                if (deleted != 1)
                {
                    return NotFound(new ResponseDto { IsSuccess = false, Message = "Cart item not found" });
                }

                await _db.CartHeaders.Where(header => header.CartHeaderId == cartDetails.CartHeaderId &&
                    header.UserId == subject && !_db.CartDetails.Any(item => item.CartHeaderId == header.CartHeaderId))
                    .ExecuteDeleteAsync();
                await transaction.CommitAsync();

                _response.Result = true;
            }
            catch (Exception)
            {
                _response.Message = "An unexpected error occurred";
                _response.IsSuccess = false;
                return StatusCode(500, _response);
            }
            return _response;
        }

        [HttpPost("EmailCartRequest")]
        public async Task<ActionResult<ResponseDto>> EmailCartRequest([FromBody] CartDto cartDto)
        {
            var userId = cartDto?.CartHeader?.UserId;
            if (!Guid.TryParseExact(userId, "D", out _) || cartDto!.CartHeader.CartHeaderId < 0)
            {
                return BadRequest(new ResponseDto { IsSuccess = false, Message = "Invalid cart identifier" });
            }

            var subject = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (!string.Equals(userId, subject, StringComparison.Ordinal))
            {
                return NotFound(new ResponseDto { IsSuccess = false, Message = "Cart not found" });
            }

            try
            {
                var header = await _db.CartHeaders.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == subject);
                if (header is null || (cartDto.CartHeader.CartHeaderId != 0 && cartDto.CartHeader.CartHeaderId != header.CartHeaderId))
                {
                    return NotFound(new ResponseDto { IsSuccess = false, Message = "Cart not found" });
                }

                var emails = User.FindAll(JwtRegisteredClaimNames.Email).ToArray();
                if (emails.Length != 1 || !MailAddress.TryCreate(emails[0].Value, out var address) ||
                    !string.Equals(address.Address, emails[0].Value, StringComparison.OrdinalIgnoreCase))
                {
                    return StatusCode(503, new ResponseDto { IsSuccess = false, Message = "Cart email is unavailable" });
                }

                var details = await _db.CartDetails.AsNoTracking().Where(x => x.CartHeaderId == header.CartHeaderId).ToListAsync();
                var products = await _productService.GetProducts();
                var message = new CartDto
                {
                    CartHeader = _mapper.Map<CartHeaderDto>(header),
                    CartDetails = _mapper.Map<List<CartDetailsDto>>(details)
                };
                message.CartHeader.Email = address.Address;
                foreach (var item in message.CartDetails)
                {
                    item.Product = products.FirstOrDefault(x => x.ProductId == item.ProductId);
                    if (item.Product is null)
                    {
                        return StatusCode(503, new ResponseDto { IsSuccess = false, Message = "Cart email is unavailable" });
                    }
                    message.CartHeader.CartTotal += item.Count * item.Product.Price;
                }

                if (!string.IsNullOrEmpty(header.CouponCode))
                {
                    var coupon = await _couponService.GetCoupon(header.CouponCode);
                    if (coupon is null)
                    {
                        return StatusCode(503, new ResponseDto { IsSuccess = false, Message = "Cart email is unavailable" });
                    }
                    if (message.CartHeader.CartTotal > coupon.MinAmount)
                    {
                        message.CartHeader.CartTotal -= coupon.DiscountAmount;
                        message.CartHeader.Discount = coupon.DiscountAmount;
                    }
                }

                await _messageBus.PublishMessage(message, _configuration.GetValue<string>("TopicAndQueueNames:EmailShoppingCartQueue"));
                _response.Result = true;
            }
            catch (Exception)
            {
                _response.IsSuccess = false;
                _response.Message = "An unexpected error occurred";
                return StatusCode(500, _response);
            }
            return _response;
        }
    }
}
