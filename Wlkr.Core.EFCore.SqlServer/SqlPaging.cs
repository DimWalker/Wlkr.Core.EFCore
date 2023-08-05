using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Wlkr.Core.EFCore.SqlServer
{
    /// <summary>
    /// 配合参数化的SqlFormatter一起使用
    /// 一般必填项为: 
    /// Select = ""
    /// From = ""
    /// Where = SqlFormatter.FormatedSql
    /// ParamList = SqlFormatter.pmLst
    /// OrderBy = ""
    /// db = _dbcontext
    /// </summary>
    public class SqlPaging
    {
        /*
         * 主要目的是配合参数化的SqlFormatter，重新改造.ner framework时期的SqlPaging
         * 
         * 1. sql count(1)
         * 2. WITH query_table AS 分页
         */

        public SqlPaging()
        {
        }
        public SqlPaging(DbContext _db)
        {
            db = _db;
        }

        /// <summary>
        /// 必要
        /// </summary>
        public DbContext db { get; set; }

        /// <summary>
        /// 必要，格式不带Select，如" field1,field2"
        /// </summary>
        public string Select { get; set; }
        /// <summary>
        /// 必要，格式不到From，如" table1 left join table 2"
        /// </summary>
        public string From { get; set; }

        public string Where { get { return WhereBuilder.FormatedSql; } }
        public SqlParameter[] ParamList { get { return WhereBuilder.Parameters; } }
        public SqlFormatter WhereBuilder { get; set; } = new SqlFormatter();

        /// <summary>
        /// 必要，格式不带Order by，如" field1 "
        /// 用于分页
        /// </summary>
        public string OrderBy { get; set; }

        /// <summary>
        /// 必要，分页信息
        /// </summary>
        public PagingUtil PagingUtil { get; set; } = new PagingUtil(10,1);



        public IEnumerable<dynamic> Execute()
        {
            Execute_Count();
            return db.SqlQueryDynamic(Sql, ParamList);
        }
        public List<T> Execute<T>()
        {
            Execute_Count();
            return db.SqlQueryDynamic<T>(Sql, ParamList);
        }

        private void Check()
        {
            if (db == null)
                throw new ArgumentNullException(nameof(db));
            if (string.IsNullOrEmpty(Select))
                throw new ArgumentNullException(nameof(Select));
            if (string.IsNullOrEmpty(From))
                throw new ArgumentNullException(nameof(From));
            if (string.IsNullOrEmpty(OrderBy))
                throw new ArgumentNullException(nameof(OrderBy));
        }
        private void Execute_Count()
        {
            object r = db.SqlScalar(SqlCnt, ParamList);
            int cnt = int.Parse(r.ToString());
            PagingUtil.CalcPageParams(cnt);
        }

        public string SqlCnt
        {
            get
            {
                return @"Select Count(1) 
From " + From + @" 
Where 1=1 " + Where;
            }
        }
        public string Sql
        {
            get
            {
                return $@"WITH query_table AS
(
	SELECT row_number() over (order by {OrderBy} ) AS  rownumber,
	{Select} 
	FROM {From} 
	WHERE 1=1 {Where} 
)
SELECT * from query_table 
WHERE rownumber between {PagingUtil.Skip + 1} and {PagingUtil.Skip + PagingUtil.PageSize}
";
            }
        }
    }
}
