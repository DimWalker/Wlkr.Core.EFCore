[![DimTechStudio.Com](vx_iamges/DimTechStudio-Logo.png)](https://www.dimtechstudio.com/)

# Wlkr.Core.EFCore
逆天，我在ef core中使用ado.net！

# 老派Sql之必要
* 当你开发生涯中基本只用一两种数据库
* 当你觉得用EF的类写报表时很别扭
* 当你觉自己的Sql( Server)语句写得出神入化
* 当你觉自己的Sql( Server)语句比EF生成的更优化
* 当你刚从.net framework转.net core，还不知道sqlsugar和dapper

如上面所说，本项目在几年前，笔者刚转到.net core 3.1的开发中，编写了此项目。  
当时觉得ef core这的很强大，编写业务代码时，效率提升极高，层次结构、逻辑代码都很清晰统一。  
但是到了开发报表时，多表关联，奇葩条件组合就显示很别扭，各种奇怪的select，join，where等操作用于类中，令人抓狂，在sql语句中可能几行能写完的东西，夸张点可能得写上几十行，即便有linq和lambda的辅助，也没有直接写sql好用。
于是便诞生了此项目。

# 食用方式
`EFCoreQueryHelper`此类基本功能分为SqlQuery，SqlNonQuery，SqlScalar，Reader及DataSet。
每个类型有3中接口
* 第一种直接使用`string`，不能防止SQL注入。
* 第二种使用`FormattableString`，能防止SQL注入
* 第三种我封装的`SqlFormatter`，实现像StringBuilder一样拼接`FormattableString`。

Console项目“EFCoreSample”里面有一些使用示例
## 环境准备
* 你需要Sql server，.net6 SDK
* 在Console中默认没有从appsettings.json读取config的功能，此处我们自己先构造一个config。
```C#
//读取Config
var configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory) // 设置基础路径为应用程序根目录
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true) // 加载 appsettings.json 文件
        .AddEnvironmentVariables() // 可以添加环境变量的配置
        .AddCommandLine(args) // 可以添加命令行参数的配置
        .Build();
```
* 然后构造一个DbContext  
<mark>平时都是web api项目里注入，Console差点不会写……</mark>
```C#
//配置DbContext
EFCoreQueryHelper.Configuration = configuration;
string constr = "Default";
DbContextOptionsBuilder<EFCoreSampleDbContext> optionsBuilder = new DbContextOptionsBuilder<EFCoreSampleDbContext>
    ();
optionsBuilder.UseSqlServer(configuration.GetConnectionString(constr));
using EFCoreSampleDbContext dbContext = new EFCoreSampleDbContext(optionsBuilder.Options);
```
* 再执行迁移及种子数据
```C#
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
```

测试环境已准备好，下面进入正题，该如何使用。
## 如何防SQL注入
防SQL注入的核心，是使用`FormattableString`，从中提取格式化的字符串，及其参数变量，从而转换为SqlParameter[]，在ado.net里执行。

### 使用`FormattableString`
* 方法名带Interpolated后缀的，都是防SQL注入
```C#
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
```
* 要注意区分`FormattableString`($"")和`string`("")的使用
下面方法不带Interpolated，虽然也是用了`FormattableString`，但它最终会是`string`，没法防SQL注入。
在传统的ado.net中，下面的{id}应该改为@p0，参数化才能防SQL注入。
```C#
//以下是纯粹的拼接字符串，不能防Sql注入，不推荐
full = dbContext.SqlQueryDynamic<TestModel>($"select * from TestModel where id = {id}", new SqlParameter[] { });
Console.WriteLine("不防注入:");
Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(full));
Console.WriteLine();
```

### 使用`SqlFormatter`

`$""` 的缺点是不能拼接如`$"" + $""`，它会变为`string`，导致不能防SQL注入。
此时需要想StringBuilder一样，封装一个可以Append字符串的类。
```C#
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
```
可以看到`sqlFormatter.FormatedSql`中已经将{p}转化为@p。

### 使用`SqlPaging`
报表开发中用的最多的当然是分页了，结合`SqlFormatter`、`SqlPaging`、`PagingUtil`，实现分页查询。
```C#
//分页例子
Console.WriteLine("SqlPaging分页:");
//先构建条件，约等于id为奇数的数据
sqlFormatter = new SqlFormatter();
sqlFormatter.AppendLine_FmtStr($"and t.B = {false}");

//除了等号逗号，写法基本与sql语句一致
SqlPaging sqlPaging = new SqlPaging()
{
    db = dbContext,
    Select = "*",
    From = "TestModel t",
    WhereBuilder = sqlFormatter,
    OrderBy = " t.id desc"
};

//每页10条
full = sqlPaging.Execute<TestModel>();
Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(sqlPaging.PagingUtil));
Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(full));
Console.WriteLine();

//每页5条，第三页
sqlPaging.PagingUtil.PageSize = 5;
sqlPaging.PagingUtil.PageIdx = 3;
full = sqlPaging.Execute<TestModel>();
Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(sqlPaging.PagingUtil));
Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(full));
Console.WriteLine();
```

## EF、SqlSugar、Dapper对比
除了开始不知道后面两个外，我喜欢EF的Migration功能，它比SqlSugar强。
而且使用EF，显得相当清真，它又是我自己写，自己用的类库，它不需要它复杂的功能，满足我日常使用即可。
反射方面的性能没有对比，暂略。

## 其他
既然是老派SQL，当然少不了DbFirst。
后续补充DbFirst转CodeFirst开发模式。
To Be Continue...

## Author Info
DimWalker
©2023 广州市增城区黯影信息科技部
[https://www.dimtechstudio.com/](https://www.dimtechstudio.com/)