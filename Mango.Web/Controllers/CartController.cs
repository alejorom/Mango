using Mango.Web.Models;
using Mango.Web.Service.IService;
using Mango.Web.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;

namespace Mango.Web.Controllers
{
    public class CartController : Controller
    {
        private readonly ICartService _cartService;
        private readonly IOrderService _orderService;
        public CartController(ICartService cartService, IOrderService orderService)
        {
            _cartService = cartService;
            _orderService = orderService;
        }

        [Authorize]
        public async Task<IActionResult> CartIndex()
        {
            var cart = await LoadCartDtoBasedOnLoggedInUser();
            return cart == null ? StatusCode(Response.StatusCode) : View(cart);
        }

        public IActionResult Index()
        {
            return View();
        }

        private async Task<CartDto?> LoadCartDtoBasedOnLoggedInUser()
        {
            var userId = User.Claims.Where(u => u.Type == JwtRegisteredClaimNames.Sub)?.FirstOrDefault()?.Value;
            ResponseDto? response = await _cartService.GetCartByUserIdAsnyc(userId);
            if (response != null && response.IsSuccess && response.Result != null)
            {
                CartDto cartDto = JsonConvert.DeserializeObject<CartDto>(Convert.ToString(response.Result));
                return cartDto;
            }
            Response.StatusCode = response?.HttpStatusCode ?? StatusCodes.Status502BadGateway;
            return null;
        }

        public async Task<IActionResult> Remove(int cartDetailsId)
        {
            var userId = User.Claims.Where(u => u.Type == JwtRegisteredClaimNames.Sub)?.FirstOrDefault()?.Value;
            ResponseDto? response = await _cartService.RemoveFromCartAsync(cartDetailsId);
            if (response != null && response.IsSuccess)
            {
                TempData["success"] = "Cart updated successfully";
                return RedirectToAction(nameof(CartIndex));
            }
            return StatusCode(response?.HttpStatusCode ?? StatusCodes.Status502BadGateway);
        }

        public async Task<IActionResult> Confirmation(int orderId)
        {
            ResponseDto? response = await _orderService.ValidateStripeSession(orderId);
            if (response != null && response.IsSuccess)
            {

                OrderHeaderDto orderHeader = JsonConvert.DeserializeObject<OrderHeaderDto>(Convert.ToString(response.Result));
                if (orderHeader.Status == SD.Status_Approved)
                {
                    return View(orderId);
                }
            }
            
            // Redirect to some error page based on status
            return View(orderId);
        }

        [HttpPost]
        public async Task<IActionResult> ApplyCoupon(CartDto cartDto)
        {

            ResponseDto? response = await _cartService.ApplyCouponAsync(cartDto);
            if (response != null && response.IsSuccess)
            {
                TempData["success"] = "Cart updated successfully";
                return RedirectToAction(nameof(CartIndex));
            }
            return StatusCode(response?.HttpStatusCode ?? StatusCodes.Status502BadGateway);
        }

        [HttpPost]
        public async Task<IActionResult> RemoveCoupon(CartDto cartDto)
        {
            if (cartDto?.CartHeader == null) return BadRequest();
            cartDto.CartHeader.CouponCode = "";
            ResponseDto? response = await _cartService.ApplyCouponAsync(cartDto);
            if (response != null && response.IsSuccess)
            {
                TempData["success"] = "Cart updated successfully";
                return RedirectToAction(nameof(CartIndex));
            }
            return StatusCode(response?.HttpStatusCode ?? StatusCodes.Status502BadGateway);
        }

        [Authorize]
        public async Task<IActionResult> Checkout()
        {
            var cart = await LoadCartDtoBasedOnLoggedInUser();
            if (cart?.CartHeader == null) return StatusCode(Response.StatusCode >= 400 ? Response.StatusCode : StatusCodes.Status502BadGateway);
            if (cart.CartHeader.CartHeaderId == 0) return NotFound();
            return View(cart);
        }

        [HttpPost]
        [ActionName("Checkout")]
        public async Task<IActionResult> Checkout(CartDto cartDto)
        {

            CartDto? cart = await LoadCartDtoBasedOnLoggedInUser();
            if (cart?.CartHeader == null) return StatusCode(Response.StatusCode >= 400 ? Response.StatusCode : StatusCodes.Status502BadGateway);
            if (cart.CartHeader.CartHeaderId == 0) return NotFound();
            cart.CartHeader.Phone = cartDto.CartHeader.Phone;
            cart.CartHeader.Email = cartDto.CartHeader.Email;
            cart.CartHeader.Name = cartDto.CartHeader.Name;

            var response = await _orderService.CreateOrder(cart);
            OrderHeaderDto orderHeaderDto = JsonConvert.DeserializeObject<OrderHeaderDto>(Convert.ToString(response.Result));

            if (response != null && response.IsSuccess)
            {
                // Get stripe session and redirect to stripe to place order
                var domain = Request.Scheme + "://" + Request.Host.Value + "/";

                StripeRequestDto stripeRequestDto = new()
                {
                    ApprovedUrl = domain + "cart/Confirmation?orderId=" + orderHeaderDto.OrderHeaderId,
                    CancelUrl = domain + "cart/checkout",
                    OrderHeader = orderHeaderDto
                };

                var stripeResponse = await _orderService.CreateStripeSession(stripeRequestDto);
                StripeRequestDto stripeResponseResult = JsonConvert.DeserializeObject<StripeRequestDto>(Convert.ToString(stripeResponse.Result));
                Response.Headers.Add("Location", stripeResponseResult.StripeSessionUrl);
                return new StatusCodeResult(303);
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> EmailCart(CartDto cartDto)
        {
            CartDto? cart = await LoadCartDtoBasedOnLoggedInUser();
            if (cart?.CartHeader == null) return StatusCode(Response.StatusCode >= 400 ? Response.StatusCode : StatusCodes.Status502BadGateway);
            if (cart.CartHeader.CartHeaderId == 0) return NotFound();
            cart.CartHeader.Email = User.Claims.Where(u => u.Type == JwtRegisteredClaimNames.Email)?.FirstOrDefault()?.Value;
            ResponseDto? response = await _cartService.EmailCart(cart);
            if (response != null && response.IsSuccess)
            {
                TempData["success"] = "Email will be processed and sent shortly.";
                return RedirectToAction(nameof(CartIndex));
            }
            return StatusCode(response?.HttpStatusCode ?? StatusCodes.Status502BadGateway);
        }
    }
}
