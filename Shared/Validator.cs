using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Shared
{
    public static class Validator
    {
        public static T Validate<T>(string setting)
        {
            _ = setting ?? throw new ArgumentNullException(nameof(setting));

            var type = typeof(T);

            if (type == typeof(string))
            {
                if (string.IsNullOrEmpty(setting))
                {
                    return default;
                }

                return (T)Convert.ChangeType(setting, typeof(T));
            }
            else if (type == typeof(int))
            {
                try
                {
                    _ = int.Parse(setting);
                }
                catch (Exception)
                {
                    return default;
                }

                return (T)Convert.ChangeType(setting, typeof(T));
            }
            else if (type == typeof(IPAddress))
            {
                try
                {
                    _ = IPAddress.Parse(setting);
                }
                catch (Exception)
                {
                    return default;
                }

                return (T)Convert.ChangeType(IPAddress.Parse(setting), typeof(T));
            }
            else if (type == typeof(List<string>))
            {
                var split = setting.Split(",");
                var list = new List<string>();

                foreach (var program in split)
                {
                    list.Add(program.Replace(".exe", ""));
                }

                return (T)Convert.ChangeType(list.Where(program => program != "").ToList(), typeof(T));
            }
            else if (type == typeof(bool))
            {
                _ = bool.TryParse(setting, out var boolValue);

                return (T)Convert.ChangeType(boolValue, typeof(T));
            }

            throw new NotImplementedException(typeof(T).ToString());
        }
    }
}
