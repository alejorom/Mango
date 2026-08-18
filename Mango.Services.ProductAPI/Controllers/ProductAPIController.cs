using AutoMapper;
using Mango.Services.ProductAPI.Data;
using Mango.Services.ProductAPI.Models;
using Mango.Services.ProductAPI.Models.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Mango.Services.ProductAPI.Controllers
{
    [Route("api/product")]
    [ApiController]
    public class ProductAPIController : ControllerBase
    {
        private readonly AppDbContext _db;
        private ResponseDto _response;
        private IMapper _mapper;

        public ProductAPIController(AppDbContext db, IMapper mapper)
        {
            _db = db;
            _mapper = mapper;
            _response = new ResponseDto();
        }

        [HttpGet]
        public async Task<ActionResult<ResponseDto>> Get()
        {
            try
            {
                IEnumerable<Product> objList = await _db.Products.ToListAsync();
                _response.Result = _mapper.Map<IEnumerable<ProductDto>>(objList);
            }
            catch (Exception)
            {
                _response.IsSuccess = false;
                _response.Message = "An unexpected error occurred";
                return StatusCode(500, _response);
            }
            return _response;
        }

        [HttpGet]
        [Route("{id:int}")]
        public async Task<ActionResult<ResponseDto>> Get(int id)
        {
            try
            {
                Product obj = await _db.Products.FirstOrDefaultAsync(u => u.ProductId == id);
                if (obj == null)
                {
                    _response.IsSuccess = false;
                    _response.Message = "Product not found";
                    return NotFound(_response);
                }
                _response.Result = _mapper.Map<ProductDto>(obj);
            }
            catch (Exception)
            {
                _response.IsSuccess = false;
                _response.Message = "An unexpected error occurred";
                return StatusCode(500, _response);
            }
            return _response;
        }

        [HttpPost]
        [Authorize(Roles = "ADMIN")]
        public async Task<ActionResult<ResponseDto>> Post([FromBody] ProductDto productDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                Product obj = _mapper.Map<Product>(productDto);
                _db.Products.Add(obj);
                await _db.SaveChangesAsync();

                _response.Result = _mapper.Map<ProductDto>(obj);
            }
            catch (Exception)
            {
                _response.IsSuccess = false;
                _response.Message = "An unexpected error occurred";
                return StatusCode(500, _response);
            }
            return _response;
        }

        [HttpPut]
        [Authorize(Roles = "ADMIN")]
        public async Task<ActionResult<ResponseDto>> Put([FromBody] ProductDto productDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                Product obj = await _db.Products.FirstOrDefaultAsync(u => u.ProductId == productDto.ProductId);
                if (obj == null)
                {
                    _response.IsSuccess = false;
                    _response.Message = "Product not found";
                    return NotFound(_response);
                }

                _mapper.Map(productDto, obj);
                await _db.SaveChangesAsync();

                _response.Result = _mapper.Map<ProductDto>(obj);
            }
            catch (Exception)
            {
                _response.IsSuccess = false;
                _response.Message = "An unexpected error occurred";
                return StatusCode(500, _response);
            }
            return _response;
        }

        [HttpDelete]
        [Route("{id:int}")]
        [Authorize(Roles = "ADMIN")]
        public async Task<ActionResult<ResponseDto>> Delete(int id)
        {
            try
            {
                Product obj = await _db.Products.FirstOrDefaultAsync(u => u.ProductId == id);
                if (obj == null)
                {
                    _response.IsSuccess = false;
                    _response.Message = "Product not found";
                    return NotFound(_response);
                }
                _db.Products.Remove(obj);
                await _db.SaveChangesAsync();
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