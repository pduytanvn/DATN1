namespace CoffeeCRM.Core.Util
{
    public static class CustomQuery
    {
        public static string ToCustomStringA(this DateTime date)
        {
            return date.ToString("dd/MM/yyyy HH:mm:ss");
        }
        public static string ToDateStringA(this DateTime date)
        {
            return date.ToString("dd/MM/yyyy");
        }
    }
}
