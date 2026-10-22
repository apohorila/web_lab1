using System.Globalization;
using System.Net;
using System.Text;

namespace server.Integrations.Telegram;

public record OrderLine(string Name, int Quantity, decimal UnitPrice);

public record OrderNotification(
    int OrderId,
    string CustomerName,
    string Phone,
    string Email,
    string City,
    string Address,
    decimal Total,
    IReadOnlyList<OrderLine> Lines,
    bool IsGuest
);


public static class TelegramMessages
{
    private const int MaxLines = 15; 

    public static string NewOrder(OrderNotification order)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"🛍 <b>Нове замовлення #{order.OrderId}</b>");
        AppendDetails(sb, order);
        return sb.ToString().TrimEnd();
    }

    public static string OrderCancelled(OrderNotification order)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"❌ <b>Замовлення #{order.OrderId} скасовано</b>");
        AppendDetails(sb, order);
        return sb.ToString().TrimEnd();
    }

    public static string Test() => "✅ <b>Bagelle</b>: Telegram-сповіщення налаштовано правильно.";

    private static void AppendDetails(StringBuilder sb, OrderNotification order)
    {
        sb.AppendLine($"👤 {E(order.CustomerName)}{(order.IsGuest ? " (гість)" : string.Empty)}");
        sb.AppendLine($"📞 {E(order.Phone)}");
        if (!string.IsNullOrWhiteSpace(order.Email))
        {
            sb.AppendLine($"✉️ {E(order.Email)}");
        }
        sb.AppendLine($"📍 {E(order.City)}, {E(order.Address)}");
        sb.AppendLine();

        foreach (var line in order.Lines.Take(MaxLines))
        {
            sb.AppendLine(
                $"• {E(line.Name)} × {line.Quantity} — {Money(line.UnitPrice * line.Quantity)}"
            );
        }
        if (order.Lines.Count > MaxLines)
        {
            sb.AppendLine($"… і ще {order.Lines.Count - MaxLines} поз.");
        }

        sb.AppendLine();
        sb.Append($"💰 <b>Разом: {Money(order.Total)}</b>");
    }

    
    private static string E(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);

    
    private static string Money(decimal value) =>
        "₴" + value.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', ' ');
}
