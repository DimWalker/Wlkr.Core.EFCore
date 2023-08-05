using System;
using System.Collections.Generic;
using System.Text;

namespace Wlkr.Core.EFCore.SqlServer
{
    public class SqlUtil
    {
        /// <summary>
        /// 查询条件时，将这5个字符转义 ' [ % _ ^，insert/update时，将' 转义 
        /// </summary>
        /// <param name="strValue"></param>
        /// <param name="isWhere">是否查询条件字段</param>
        /// <returns></returns>
        public static string SafeSql(string strValue, bool isWhere = true)
        {
            var s = strValue.Replace("'", "''");
            if (isWhere)
            {
                s = s.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]").Replace("^", "[^]");
            }
            return s;
        }
    }
}
