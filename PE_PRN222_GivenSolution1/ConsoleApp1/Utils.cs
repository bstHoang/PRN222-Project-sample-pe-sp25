using System.Reflection;


namespace ConsoleApp1
{
    public class Utils
    {
        // Format object method
        public static string FormatObject<T>(T item)
        {
            if (item == null) return string.Empty;

            PropertyInfo[] properties = typeof(T).GetProperties();
            string result = "";

            foreach (var prop in properties)
            {
                result += $"{prop.Name}: {prop.GetValue(item)} ";
            }

            return result.Trim();
        }
    }
}