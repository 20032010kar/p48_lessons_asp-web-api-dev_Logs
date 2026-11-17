using AutoMapper;
using AutoMapper.QueryableExtensions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using p48_lessons_asp.Data;
using p48_lessons_asp.Data.Entity;
using p48_lessons_asp.DTO.Product;
using p48_lessons_asp.Models;
using p48_lessons_asp.Models.ProductRequestModels;
using p48_lessons_asp.Services.FluentValidation;
using p48_lessons_asp.Services.Slug;

namespace p48_lessons_asp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly DataContext _dataContext;
        private readonly IValidator<CreateProductModel> _validator;
        private readonly ISlugService _slugService;
        private readonly IMapper _mapper;

        public ProductsController(
            DataContext dataContext,
            IValidator<CreateProductModel> validator,
            ISlugService slugService,
            IMapper mapper)
        {
            _dataContext = dataContext;
            _validator = validator;
            _slugService = slugService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts()
        {
            try
            {
                var products = await _dataContext.Products
                    .Include(p => p.Images)
                    .ProjectTo<ProductDto>(_mapper.ConfigurationProvider)
                    .ToListAsync();

                if (products == null || products.Count == 0)
                {
                    return StatusCode(404, new ResponseModel<object>
                    {
                        StatusCode = 404,
                        Message = "Products not found"
                    });
                }

                return StatusCode(200, new ResponseModel<List<ProductDto>>
                {
                    StatusCode = 200,
                    Message = "Ok",
                    Data = products
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ResponseModel<object>
                {
                    StatusCode = 500,
                    Message = $"Internal server error: {ex.Message}"
                });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateProduct([FromForm] CreateProductModel model)
        {
            var validationResult = await _validator.GetErrorsAsync(model);
            if (validationResult != null)
            {
                return new ObjectResult(validationResult) { StatusCode = 400 };
            }

            var product = new Data.Entity.Product
            {
                Name = model.Name,
                Slug = _slugService.GetSlug(model.Name),
                CategoryId = model.CategoryId,
                Price = model.Price,
                Description = model.Description
            };

            if (model.ProductImage != null)
            {
                var imageName = Guid.NewGuid().ToString() + Path.GetExtension(model.ProductImage.FileName);
                var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");

                if (!Directory.Exists(uploadFolder))
                {
                    Directory.CreateDirectory(uploadFolder);
                }

                var filePath = Path.Combine(uploadFolder, imageName);
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ProductImage.CopyToAsync(stream);
                }

                product.Images = new List<ProductImage>
                {
                    new ProductImage
                    {
                        Url = $"/images/{imageName}"
                    }
                };
            }

            try
            {
                await _dataContext.Products.AddAsync(product);
                await _dataContext.SaveChangesAsync();

                return StatusCode(201, new
                {
                    StatusCode = 201,
                    Message = "Product successfully created",
                    Data = _mapper.Map<ProductDto>(product)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(503, new
                {
                    StatusCode = 503,
                    Message = $"Service is unavailable: {ex.Message}",
                    Data = (object)null!
                });
            }
        }
    }
}