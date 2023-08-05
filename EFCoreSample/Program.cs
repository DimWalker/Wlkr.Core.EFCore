// See https://aka.ms/new-console-template for more information
using EFCoreSample.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using Wlkr.Core.EFCore;
using Wlkr.Core.EFCore.SqlServer;

#region DBContext初始化
//读取Config
var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory) // 设置基础路径为应用程序根目录
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true) // 加载 appsettings.json 文件
        .AddEnvironmentVariables() // 可以添加环境变量的配置
        .AddCommandLine(args) // 可以添加命令行参数的配置
        .Build();
//配置DbContext
EFCoreQueryHelper.Configuration = configuration;
string constr = "Default";
DbContextOptionsBuilder<EFCoreSampleDbContext> optionsBuilder = new DbContextOptionsBuilder<EFCoreSampleDbContext>
    ();
optionsBuilder.UseSqlServer(configuration.GetConnectionString(constr));
using EFCoreSampleDbContext dbContext = new EFCoreSampleDbContext(optionsBuilder.Options);
//自动迁移
if (dbContext.Database.GetPendingMigrations().Any())
    dbContext.Database.Migrate();
//种子数据
void AddSeedData()
{
    if (dbContext.TestModels.Any(t => t.Id == 1))
        return;
    for (var i = 1; i < 100; i++)
    {
        TestModel testModel = new TestModel()
        {
            //Id = i,
            S = "S" + i.ToString(),
            L = i,
            B = i % 2 == 0 ? true : false,
            D = i,
            F = i,
            G = Guid.NewGuid(),
            CreateDate = DateTime.Now
        };
        dbContext.Add(testModel);
    }
    dbContext.SaveChanges();
}
AddSeedData();
#endregion


int id = 1;

//方法名带Interpolated后缀的，是会将$""字符串转化为SqlParameter[]的参数化字符串组合。可以方式Sql注入

//查询示例
Console.WriteLine("防注入:");
List<TestModel> full = dbContext.SqlQueryDynamicInterpolated<TestModel>($"select * from TestModel where id = {id}");
Console.WriteLine("full:");
Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(full));
Console.WriteLine();
//部分字段
List<TestModel> part = dbContext.SqlQueryDynamicInterpolated<TestModel>($"select S from TestModel where id = {id}");
Console.WriteLine("part:");
Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(part));
Console.WriteLine();
//动态对象，本质是ExpandoObject
IEnumerable<dynamic> dyn = dbContext.SqlQueryDynamicInterpolated($"select S from TestModel where id = {id}");
Console.WriteLine("dyn:");
Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(dyn));
Console.WriteLine();

//以下是纯粹的拼接字符串，不能防Sql注入，不推荐
full = dbContext.SqlQueryDynamic<TestModel>($"select * from TestModel where id = {id}", new SqlParameter[] { });
Console.WriteLine("不防注入:");
Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(full));
Console.WriteLine();

//$""的缺点是不能拼接如$"" + $""，它会变为字符串而非FormattableString，从而导致SqlQueryDynamicInterpolated的防注入功能失效。
//此时需要使用SqlFormatter
Console.WriteLine("SqlFormatter拼接:");
SqlFormatter sqlFormatter = new SqlFormatter("select * from TestModel where 1=1 ");
//参数化条件
if (true)
    sqlFormatter.AppendLine_FmtStr($"and id = {id}");
//不需要参数化的条件
if (true)
    sqlFormatter.AppendLine_Str("and S = 'S1'");
//主要看{}，这也是参数化
if (true)
    sqlFormatter.AppendLine_FmtStr($"and S = {"S" + id}");
sqlFormatter.AppendLine_FmtStr($"or L = {2}");
sqlFormatter.AppendLine_FmtStr($"or F = {3}");
sqlFormatter.AppendLine_FmtStr($"or D = {4}");
sqlFormatter.AppendLine_FmtStr($"or (B = {false} and id in (5,7,9) )");
Console.WriteLine(sqlFormatter.FormatedSql);
full = dbContext.SqlQueryDynamic<TestModel>(sqlFormatter.FormatedSql, sqlFormatter.Parameters);
Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(full));
Console.WriteLine();

//分页例子
Console.WriteLine("SqlPaging分页:");
sqlFormatter = new SqlFormatter();
sqlFormatter.AppendLine_FmtStr($"and t.B = {false}");
SqlPaging sqlPaging = new SqlPaging()
{
    db = dbContext,
    Select = "*",
    From = "TestModel t",
    WhereBuilder = sqlFormatter,
    OrderBy = " t.id desc"
};
full = sqlPaging.Execute<TestModel>();
Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(sqlPaging.PagingUtil));
Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(full));
Console.WriteLine();
sqlPaging.PagingUtil.PageSize = 5;
sqlPaging.PagingUtil.PageIdx = 3;
full = sqlPaging.Execute<TestModel>();
Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(sqlPaging.PagingUtil));
Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(full));
Console.WriteLine();