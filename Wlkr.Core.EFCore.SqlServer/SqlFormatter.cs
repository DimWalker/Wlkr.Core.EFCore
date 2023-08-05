using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Text;

namespace Wlkr.Core.EFCore
{
    public class SqlFormatter
    {
        /// <summary>
        /// 如需构造参数化的where，可使用重载方法new SqlFormatter(string baseSql)
        /// </summary>
        public SqlFormatter()
        {
        }
        public string FormatedSql { get; set; } = "";
        /// <summary>
        /// 早期实现。
        /// 如果调用过AppendLine增加where条件，请使用pmLst.ToArray()
        /// </summary>       
        public SqlParameter[] Parameters { get { return ParameterList.ToArray(); } }

        /// <summary>
        /// 主要目的是构造参数化的where，防止sql注入
        /// </summary>
        /// <param name="baseSql">如果需要关联使用SqlPaging，这里填空""
        /// 如果不需要，这里可以传入SQL语句如
        /// @"select field
        /// from table
        /// where 1=1 "
        /// </param>
        public SqlFormatter(string baseSql)
        {
            FormatedSql = baseSql;
        }

        private List<SqlParameter> _ParameterList;
        /// <summary>
        /// 调用过AppendLine_FmtStr后，使用pmLst.ToArray()获取参数
        /// </summary>
        public List<SqlParameter> ParameterList
        {
            get
            {
                if (_ParameterList == null)
                    _ParameterList = new List<SqlParameter>();
                return _ParameterList;
            }
            set
            {
                _ParameterList = value;
            }
        }
        /// <summary>
        /// 格式如 and x=1
        /// </summary>
        /// <param name="partSql"></param>
        public void AppendLine_FmtStr(FormattableString partSql)
        {
            var args = partSql.GetArguments();
            var fmt = partSql.Format;
            for (int i = 0; i < args.Length; i++)
            {
                string pn = "@p" + ParameterList.Count.ToString();
                ParameterList.Add(new SqlParameter(pn, args[i]));
                fmt = fmt.Replace("{" + i.ToString() + "}", pn);
            }
            FormatedSql += "\r\n" + fmt;
        }
        /// <summary>
        /// 格式如 and x=1
        /// </summary>
        /// <param name="partSql"></param>
        public void AppendLine_Str(string partSql)
        {
            FormatedSql += "\r\n" + partSql;
        }
    }
}
