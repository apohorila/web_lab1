using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using server.Data;
using server.Models;

namespace server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShowroomsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ShowroomsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Showroom>>> GetShowrooms()
    {
        return await _context.Showrooms.AsNoTracking().ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Showroom>> GetShowroom(int id)
    {
        var showroom = await _context.Showrooms.FindAsync(id);
        if (showroom == null)
            return NotFound();

        return Ok(showroom);
    }

    [HttpPost]
    public async Task<ActionResult<Showroom>> CreateShowroom(Showroom showroom)
    {
        _context.Showrooms.Add(showroom);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetShowroom), new { id = showroom.Id }, showroom);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateShowroom(int id, Showroom showroom)
    {
        if (id != showroom.Id)
            return BadRequest();

        var existing = await _context.Showrooms.FindAsync(id);
        if (existing == null)
            return NotFound();

        existing.Name = showroom.Name;
        existing.Address = showroom.Address;
        existing.WorkingHours = showroom.WorkingHours;
        existing.Phone = showroom.Phone;
        existing.Latitude = showroom.Latitude;
        existing.Longitude = showroom.Longitude;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteShowroom(int id)
    {
        var showroom = await _context.Showrooms.FindAsync(id);
        if (showroom == null)
            return NotFound();

        _context.Remove(showroom);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
