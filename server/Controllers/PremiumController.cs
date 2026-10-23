using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using server.Features;

namespace server.Controllers;

[ApiController]
[Route("api/premium")]
public class PremiumController : ControllerBase
{

    [Authorize]
    [RequireFeature(FeatureNames.PremiumLounge)]
    [HttpGet("lounge")]
    public IActionResult GetLounge() =>
        Ok(
            new
            {
                title = "Bagelle Club",
                welcome = "Вітаємо в закритому клубі Bagelle — дякуємо, що ви з нами",
                promoCode = "CLUB10",
                discountPercent = 10,
                perks = new[]
                {
                    "Ранній доступ до нових колекцій за 48 годин до релізу",
                    "Персональна знижка 10% на всі вироби (промокод нижче)",
                    "Безкоштовна доставка без мінімальної суми замовлення",
                    "Безкоштовний догляд і реставрація шкіряних виробів",
                    "Запрошення на закриті покази у шоурумах",
                },
                upcoming = new[]
                {
                    new { name = "Осіння капсула", date = "2026-11-01", note = "Шкіра наппа, 6 моделей" },
                    new { name = "Зимова лімітка", date = "2026-12-05", note = "Лімітована серія, 100 шт." },
                },
            }
        );
}
