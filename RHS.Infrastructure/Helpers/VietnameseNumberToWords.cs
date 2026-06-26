namespace RHS.Infrastructure.Helpers;

/// <summary>
/// Chuyển số tiền (decimal) sang chuỗi chữ tiếng Việt.
/// Ví dụ: 50000000 → "Năm mươi triệu"
/// </summary>
public static class VietnameseNumberToWords
{
    private static readonly string[] Units =
        { "", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };

    private static readonly string[] Tens =
        { "", "mười", "hai mươi", "ba mươi", "bốn mươi", "năm mươi",
          "sáu mươi", "bảy mươi", "tám mươi", "chín mươi" };

    /// <summary>
    /// Chuyển số nguyên dương sang chữ tiếng Việt.
    /// </summary>
    public static string Convert(decimal amount)
    {
        var number = (long)amount;

        if (number == 0) return "Không";
        if (number < 0) return "Âm " + Convert(-amount);

        var result = "";

        // Tỷ
        if (number >= 1_000_000_000)
        {
            result += ConvertGroup((int)(number / 1_000_000_000)) + " tỷ ";
            number %= 1_000_000_000;
        }

        // Triệu
        if (number >= 1_000_000)
        {
            result += ConvertGroup((int)(number / 1_000_000)) + " triệu ";
            number %= 1_000_000;
        }

        // Nghìn
        if (number >= 1_000)
        {
            result += ConvertGroup((int)(number / 1_000)) + " nghìn ";
            number %= 1_000;
        }

        // Đơn vị (0-999)
        if (number > 0)
        {
            result += ConvertGroup((int)number);
        }

        // Viết hoa chữ cái đầu, trim khoảng trắng thừa
        result = result.Trim();
        if (result.Length > 0)
        {
            result = char.ToUpper(result[0]) + result[1..];
        }

        return result;
    }

    /// <summary>
    /// Chuyển nhóm 3 chữ số (0–999) sang chữ.
    /// </summary>
    private static string ConvertGroup(int number)
    {
        if (number == 0) return "";

        var result = "";

        var hundreds = number / 100;
        var remainder = number % 100;
        var tens = remainder / 10;
        var units = remainder % 10;

        // Hàng trăm
        if (hundreds > 0)
        {
            result += Units[hundreds] + " trăm ";
        }

        // Hàng chục
        if (tens > 0)
        {
            result += Tens[tens] + " ";

            // Hàng đơn vị (sau hàng chục)
            if (units == 1)
                result += "mốt";
            else if (units == 5)
                result += "lăm";
            else if (units > 0)
                result += Units[units];
        }
        else if (units > 0)
        {
            // Có hàng trăm nhưng không có hàng chục
            if (hundreds > 0)
                result += "lẻ ";

            if (units == 5 && hundreds > 0)
                result += "lăm";
            else
                result += Units[units];
        }

        return result.Trim();
    }
}
