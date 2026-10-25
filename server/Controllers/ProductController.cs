using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Caching;
using server.Data;
using server.Models;
using server.Paging;

namespace server.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ProductsController(AppDbContext context)
    {
        _context = context;
    }

    [CacheResponse("products", 60)]
    [HttpGet]
    public async Task<ActionResult<object>> GetProducts(
       [FromQuery] int? categoryId = null,
       [FromQuery] int? skip = null,
       [FromQuery] int? limit = null
   )
    {
        var query = _context.Products.Include(p => p.Category).Include(p => p.Showroom).AsNoTracking();

        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        query = query.OrderBy(p => p.Id);

        
        if (skip == null && limit == null)
        {
            return new ActionResult<object>(await query.ToListAsync());
        }

        if (!PagingHelper.TryNormalize(skip, limit, out var pageSkip, out var pageLimit, out var error))
        {
            return BadRequest(new { message = error });
        }

        var total = await query.CountAsync();
        var items = await query.Skip(pageSkip).Take(pageLimit).ToListAsync();

        return Ok(
            new PagedResult<Product>(
                items,
                total,
                pageSkip,
                pageLimit,
                PagingHelper.BuildNextLink(Request, pageSkip, pageLimit, total)
            )
        );
    }

    [CacheResponse("products", 60)]
    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> GetProduct(int id)
    {
        var product = await _context
            .Products.Include(p => p.Category)
            .Include(p => p.Showroom)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return NotFound();
        }

        return Ok(product);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [InvalidateCache("products", "stats")]
    public async Task<ActionResult<Product>> CreateProduct([FromBody] Product product)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product); // 201
    }

    [HttpPut("{id}")]
    [InvalidateCache("products", "stats")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateProduct(int id, [FromBody] Product product)
    {
        if (id != product.Id)
        {
            return BadRequest("ID у URL не співпадає з ID тіла запиту"); // 400
        }

        _context.Entry(product).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.Products.Any(e => e.Id == id))
            {
                return NotFound();
            }
            throw;
        }

        return Ok(product);
    }

    [HttpDelete("{id}")]
    [InvalidateCache("products", "stats")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null)
        {
            return NotFound();
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
