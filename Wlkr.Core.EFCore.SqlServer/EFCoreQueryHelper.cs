using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Dynamic;
using System.Reflection;
using System.Text;

namespace Wlkr.Core.EFCore.SqlServer
{
    /// <summary>
    ///  扩展方法
    ///  静态调用写法 EFCoreQueryHelper.SqlQueryDynamic(db,sql，params)
    ///  DbContext写法 db.SqlQueryDynamic(sql，params)
    /// </summary>
    /// <remarks>
    /// 1. 因为FormattableString不能拼接，非参数化的方法还是要public
    /// 2. 改造SqlFormatter，以适应1的参数化拼接Sql
    /// </remarks>
    public static class EFCoreQueryHelper
    {
        public static IConfiguration Configuration { get; set; }

        /// <summary>
        /// 创建DbContext，记得加using或者用完记得close。
        /// </summary>
        /// <param name="connectionStringName"></param>
        /// <returns></returns>
        public static DbContext CreateDbContext(string connectionStringName)
        {
            string connectionString = ""; //AppConfig.AspNetCoreConStr;//不设默认值了
            if (!string.IsNullOrEmpty(connectionStringName))
                connectionString = Configuration.GetConnectionString(connectionStringName);
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException("connectionStringName:" + connectionStringName);

            var optionsBuilder = new DbContextOptionsBuilder<DbContext>();
            optionsBuilder.UseSqlServer(connectionString);
            return new DbContext(optionsBuilder.Options);
        }
        /// <summary>
        /// 创建DbContext，记得加using或者用完记得close。
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        public static DbContext CreateDbContextByConStr(string connectionString)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DbContext>();
            optionsBuilder.UseSqlServer(connectionString);
            return new DbContext(optionsBuilder.Options);
        }

        private static SqlFormatter FsToArray(FormattableString Sql)
        {
            var args = Sql.GetArguments();
            var fmt = Sql.Format;

            SqlParameter[] pms = new SqlParameter[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                string pn = "@p" + i.ToString();
                pms[i] = new SqlParameter(pn, args[i]);
                fmt = fmt.Replace("{" + i.ToString() + "}", pn);
            }

            return new SqlFormatter
            {
                FormatedSql = fmt,
                ParameterList = new List<SqlParameter>(pms)
            };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="db"></param>
        /// <param name="Sql"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        /// <remarks>
        /// this DbContext db, this是扩展方法的意思
        /// yield return row, yield是语法糖，正常实现IEnumerable要写很多代码
        /// </remarks>
        /// 
        public static IEnumerable<dynamic> SqlQueryDynamic(this DbContext db, string Sql, params SqlParameter[] parameters)
        {
            using (var cmd = db.Database.GetDbConnection().CreateCommand())
            {
                cmd.CommandText = Sql;
                foreach (var p in parameters)
                {
                    var dbParameter = cmd.CreateParameter();
                    dbParameter.DbType = p.DbType;
                    dbParameter.ParameterName = p.ParameterName;
                    dbParameter.Value = p.Value;
                    cmd.Parameters.Add(dbParameter);
                }
                if (cmd.Connection.State != ConnectionState.Open)
                    cmd.Connection.Open();
                using (var dataReader = cmd.ExecuteReader())
                {
                    while (dataReader.Read())
                    {
                        var row = new ExpandoObject() as IDictionary<string, object>;
                        for (var fieldCount = 0; fieldCount < dataReader.FieldCount; fieldCount++)
                        {
                            string key = dataReader.GetName(fieldCount);
                            object value = dataReader[fieldCount];
                            if (value == DBNull.Value)
                                value = null;//DBNull.Value的值是{}，会导致object有差异
                            row.Add(key, value);
                        }
                        yield return row;
                    }
                }
            }
        }
        /// <summary>
        /// 参数化防SQL注入
        /// </summary>
        /// <param name="db"></param>
        /// <param name="Sql">$"select 1 from table where str = {p1} and int = {p2}"</param>
        /// <returns></returns>
        /// <remarks>字符串、guid等参数不用带单引号</remarks>
        public static IEnumerable<dynamic> SqlQueryDynamicInterpolated(this DbContext db, FormattableString Sql)
        {
            var sp = FsToArray(Sql);
            return db.SqlQueryDynamic(sp.FormatedSql, sp.Parameters);
        }
        public static IEnumerable<dynamic> SqlQueryDynamicInterpolated(this DbContext db, SqlFormatter formatter)
        {
            return db.SqlQueryDynamic(formatter.FormatedSql, formatter.Parameters);
        }

        /// <summary>
        /// 返回泛型List
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="db"></param>
        /// <param name="Sql"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static List<T> SqlQueryDynamic<T>(this DbContext db, string Sql, params SqlParameter[] parameters)
        {
            using (var cmd = db.Database.GetDbConnection().CreateCommand())
            {
                cmd.CommandText = Sql;
                foreach (var p in parameters)
                {
                    var dbParameter = cmd.CreateParameter();
                    dbParameter.DbType = p.DbType;
                    dbParameter.ParameterName = p.ParameterName;
                    dbParameter.Value = p.Value;
                    cmd.Parameters.Add(dbParameter);
                }
                if (cmd.Connection.State != ConnectionState.Open)
                    cmd.Connection.Open();
                //获取对象所有属性
                //todo: 反射性能较差，可尝试加入到MaxSize = n的缓存，
                PropertyInfo[] propertyInfo = typeof(T).GetProperties();
                Dictionary<int, int> mapping = new Dictionary<int, int>();// dataReader[i] : propertyInfo[j]

                List<T> lst = new List<T>();
                using (var dataReader = cmd.ExecuteReader())
                {
                    for (int i = 0; i < dataReader.FieldCount; i++)
                    {
                        //对应关系
                        if (!mapping.ContainsKey(i))
                        {
                            for (int j = 0; j < propertyInfo.Length; j++)
                            {
                                //字段名
                                if (propertyInfo[j].Name.ToLower() == dataReader.GetName(i).ToLower())
                                {
                                    mapping.Add(i, j);
                                    continue;
                                }
                                //Column特性
                                var attrs = propertyInfo[j].GetCustomAttributes(typeof(ColumnAttribute));
                                //类的字段名，不一定等于表的字段名，此处通过特性匹配
                                foreach (Attribute attr in attrs)
                                {
                                    if (((ColumnAttribute)attr).Name.ToLower() == dataReader.GetName(i).ToLower())
                                    {
                                        mapping.Add(i, j);
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    while (dataReader.Read())
                    {
                        // 得到实体类对象  
                        T t = (T)Activator.CreateInstance(typeof(T));
                        for (int i = 0; i < dataReader.FieldCount; i++)
                        {
                            if (mapping.ContainsKey(i))
                            {
                                // 为了能用在默认为null的值上  
                                // 如 DateTime? tt = null;  
                                if (null == dataReader[i] || Convert.IsDBNull(dataReader[i]))
                                    propertyInfo[mapping[i]].SetValue(t, null, null);
                                else
                                    propertyInfo[mapping[i]].SetValue(t, dataReader[i], null);
                            }
                        }
                        lst.Add(t);
                    }
                }
                return lst;
            }
        }
        /// <summary>
        /// 返回泛型List
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="db"></param>
        /// <param name="Sql"></param>
        /// <returns></returns>
        public static List<T> SqlQueryDynamicInterpolated<T>(this DbContext db, FormattableString Sql)
        {
            var sp = FsToArray(Sql);
            return db.SqlQueryDynamic<T>(sp.FormatedSql, sp.Parameters);
        }
        public static List<T> SqlQueryDynamicInterpolated<T>(this DbContext db, SqlFormatter formatter)
        {
            return db.SqlQueryDynamic<T>(formatter.FormatedSql, formatter.Parameters);
        }

        public static int SqlNonQuery(this DbContext db, string Sql, params SqlParameter[] parameters)
        {
            using (var cmd = db.Database.GetDbConnection().CreateCommand())
            {
                cmd.CommandText = Sql;
                foreach (var p in parameters)
                {
                    var dbParameter = cmd.CreateParameter();
                    dbParameter.DbType = p.DbType;
                    dbParameter.ParameterName = p.ParameterName;
                    dbParameter.Value = p.Value == null ? DBNull.Value : p.Value;
                    cmd.Parameters.Add(dbParameter);
                }
                if (cmd.Connection.State != ConnectionState.Open)
                    cmd.Connection.Open();
                return cmd.ExecuteNonQuery();
            }
        }
        public static int SqlNonQueryInterpolated(this DbContext db, FormattableString Sql)
        {
            var sp = FsToArray(Sql);
            return db.SqlNonQuery(sp.FormatedSql, sp.Parameters);
        }
        public static int SqlNonQueryInterpolated(this DbContext db, SqlFormatter formatter)
        {
            return db.SqlNonQuery(formatter.FormatedSql, formatter.Parameters);
        }

        public static object SqlScalar(this DbContext db, string Sql, params SqlParameter[] parameters)
        {
            using (var cmd = db.Database.GetDbConnection().CreateCommand())
            {
                cmd.CommandText = Sql;
                foreach (var p in parameters)
                {
                    var dbParameter = cmd.CreateParameter();
                    dbParameter.DbType = p.DbType;
                    dbParameter.ParameterName = p.ParameterName;
                    dbParameter.Value = p.Value;
                    cmd.Parameters.Add(dbParameter);
                }
                if (cmd.Connection.State != ConnectionState.Open)
                    cmd.Connection.Open();
                return cmd.ExecuteScalar();
            }
        }
        public static object SqlScalarInterpolated(this DbContext db, FormattableString Sql)
        {
            var sp = FsToArray(Sql);
            return db.SqlScalar(sp.FormatedSql, sp.Parameters);
        }
        public static object SqlScalarInterpolated(this DbContext db, SqlFormatter formatter)
        {
            return db.SqlScalar(formatter.FormatedSql, formatter.Parameters);
        }

        /// <summary>
        /// 用完记得关闭DataReader
        /// </summary>
        /// <param name="db"></param>
        /// <param name="Sql"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static DbDataReader SqlReader(this DbContext db, string Sql, params SqlParameter[] parameters)
        {
            var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = Sql;
            foreach (var p in parameters)
            {
                var dbParameter = cmd.CreateParameter();
                dbParameter.DbType = p.DbType;
                dbParameter.ParameterName = p.ParameterName;
                dbParameter.Value = p.Value;
                cmd.Parameters.Add(dbParameter);
            }
            if (cmd.Connection.State != ConnectionState.Open)
                cmd.Connection.Open();
            var dataReader = cmd.ExecuteReader(CommandBehavior.CloseConnection);
            return dataReader;
        }
        /// <summary>
        /// 用完记得关闭DataReader
        /// </summary>
        /// <param name="db"></param>
        /// <param name="Sql"></param>
        /// <returns></returns>
        public static DbDataReader SqlReaderInterpolated(this DbContext db, FormattableString Sql)
        {
            var sp = FsToArray(Sql);
            return db.SqlReader(sp.FormatedSql, sp.Parameters);
        }
        public static DbDataReader SqlReaderInterpolated(this DbContext db, SqlFormatter formatter)
        {
            return db.SqlReader(formatter.FormatedSql, formatter.Parameters);
        }

        public static DataSet SqlQueryDataSet(this DbContext db, string Sql, params SqlParameter[] parameters)
        {
            using (var cmd = db.Database.GetDbConnection().CreateCommand() as SqlCommand)
            {
                cmd.CommandText = Sql;
                foreach (var p in parameters)
                {
                    var dbParameter = cmd.CreateParameter();
                    dbParameter.DbType = p.DbType;
                    dbParameter.ParameterName = p.ParameterName;
                    dbParameter.Value = p.Value;
                    cmd.Parameters.Add(dbParameter);
                }
                if (cmd.Connection.State != ConnectionState.Open)
                    cmd.Connection.Open();
                DataSet ds = new DataSet();
                SqlDataAdapter adapter = new SqlDataAdapter(cmd);
                adapter.Fill(ds);
                return ds;
            }
        }
        public static DataSet SqlQueryDataSetInterpolated(this DbContext db, FormattableString Sql)
        {
            var sp = FsToArray(Sql);
            return db.SqlQueryDataSet(sp.FormatedSql, sp.Parameters);
        }
        public static DataSet SqlQueryDataSetInterpolated(this DbContext db, SqlFormatter formatter)
        {
            return db.SqlQueryDataSet(formatter.FormatedSql, formatter.Parameters);
        }
    }
}
