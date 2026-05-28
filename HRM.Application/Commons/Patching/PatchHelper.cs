using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HRM.Application.Commons.Patching
{
    public static class PatchHelper
    {
        /// <summary>
        /// Set value when incoming is different from current.
        /// Gán giá trị khi incoming khác current.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="incoming"></param>
        /// <param name="current"></param>
        /// <param name="apply"></param>
        /// <returns></returns>
        public static bool Set<T>(
            T incoming,
            Func<T> current,
            Action<T> apply)
        {
            if (EqualityComparer<T>.Default.Equals(incoming, current()))
            {
                return false;
            }

            apply(incoming);
            return true;
        }

        /// <summary>
        /// Set Nullable struct only when incoming has value, is valid, and different
        /// Gán struct nullable khi có giá trị, hợp lệ và khác current.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="incoming"></param>
        /// <param name="current"></param>
        /// <param name="apply"></param>
        /// <param name="isValid"></param>
        /// <returns></returns>
        public static bool SetIfHasValue<T>(
            T? incoming,
            Func<T> current,
            Action<T> apply,
            Func<T, bool>? isValid = null)
            where T : struct
        {
            if (!incoming.HasValue)
            {
                return false;
            }

            if (isValid is not null && !isValid(incoming.Value))
            {
                return false;
            }

            if (EqualityComparer<T>.Default.Equals(incoming.Value, current()))
            {
                return false;
            }

            apply(incoming.Value);
            return true;
        }

        /// <summary>
        /// Set reference value only when incoming is not null and different.
        /// Gán reference type khi incoming không null và khác current.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="incoming"></param>
        /// <param name="current"></param>
        /// <param name="apply"></param>
        /// <returns></returns>
        public static bool SetIfNotNull<T>(
            T? incoming,
            Func<T?> current,
            Action<T?> apply)
            where T : class
        {
            if (incoming is null)
            {
                return false;
            }

            if (EqualityComparer<T?>.Default.Equals(incoming, current()))
            {
                return false;
            }

            apply(incoming);
            return true;
        }

        /// <summary>
        /// Set nullable struct, including setting it to null.
        /// Gán struct nullable, bao gồm cả gán về null.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="incoming"></param>
        /// <param name="current"></param>
        /// <param name="apply"></param>
        /// <returns></returns>
        public static bool SetNullable<T>(
            T? incoming,
            Func<T?> current,
            Action<T?> apply)
            where T : struct
        {
            if (Nullable.Equals(incoming, current()))
            {
                return false;
            }

            apply(incoming);
            return true;
        }

        /// <summary>
        /// Set nullable reference, including setting it to null.
        /// Gán reference type nullable, bao gồm cả gán về null.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="incoming"></param>
        /// <param name="current"></param>
        /// <param name="apply"></param>
        /// <returns></returns>
        public static bool SetNullableRef<T>(
            T? incoming,
            Func<T?> current,
            Action<T?> apply)
            where T : class
        {
            if (EqualityComparer<T?>.Default.Equals(incoming, current()))
            {
                return false;
            }

            apply(incoming);
            return true;
        }
        
        /// <summary>
        /// Set Guid value only when incoming is valid (not empty) and different from current.
        /// Gán giá trị Guid khi incoming hợp lệ (không rỗng) và khác current.
        /// <param name="incoming"></param>
        /// <param name="current"></param>
        /// <param name="apply"></param>
        /// <returns></returns>
        public static bool SetGuidIfValid(
            Guid? incoming,
            Func<Guid> current,
            Action<Guid> apply)
        {
            return SetIfHasValue(
                incoming,
                current,
                apply,
                value => value != Guid.Empty);
        }

        /// <summary>
        /// Set string value only when incoming is not null, different from current, and optionally ignore null values.
        /// Trim chuỗi trước khi gán; chuỗi trắng thành null; null mặc định được bỏ qua.
        /// </summary>
        /// <param name="incoming"></param>
        /// <param name="current"></param>
        /// <param name="apply"></param>
        /// <param name="nullMeansIgnore"></param>
        /// <returns></returns>
        public static bool SetTrimmed(
            string? incoming,
            Func<string?> current,
            Action<string?> apply,
            bool nullMeansIgnore = true)
        {
            if (incoming is null && nullMeansIgnore)
            {
                return false;
            }

            var normalized = string.IsNullOrWhiteSpace(incoming)
                ? null
                : incoming.Trim();

            if (string.Equals(normalized, current(), StringComparison.Ordinal))
            {
                return false;
            }

            apply(normalized);
            return true;
        }
    }

}
