using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using TableDependency.SqlClient;
using TableDependency.SqlClient.Base;
using TableDependency.SqlClient.Base.Enums;
using TableDependency.SqlClient.Base.EventArgs;

namespace ws.server
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            var wsOptions = new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(120) };
            app.UseWebSockets(wsOptions);

            string messages = "";

            app.Use(async (context, next) =>
            {
                if (context.Request.Path =="/send")
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        using (WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync())
                        {
                            await Send(context, webSocket, messages);
                        }
                    }
                    else
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    }
                }
            }
            );

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("Hello World!");
                });
            });
        }

        private Timer timer = new Timer();
        //public void Init()
        //{
        //    timer.Interval = 5000; // every 5 seconds
        //    timer.Tick += new EventHandler(timer_Tick);
        //    timer.Enabled = true;
        //}
        void timer_Tick(object sender, EventArgs e)
        {
            Console.WriteLine("This event will happen every 5 seconds");
        }

        private async Task Send(HttpContext context,WebSocket webSocket,string messages)
        {
            

            var buffer = new byte[1024 * 4];

            

            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), System.Threading.CancellationToken.None);
            if(result!=null)
            {
                while(!result.CloseStatus.HasValue)
                {


                    await table1(webSocket, result);
                    await table2(webSocket, result);

                    var connectionString = "Server=localhost;Database=Stock;User Id=newlocaluser;Password=password";

                    var InfoMapperOwner = new ModelToTableMapper<Owner>();
                    InfoMapperOwner.AddMapping(c => c.OwnerName, "OwnerName");
                    InfoMapperOwner.AddMapping(c => c.Shopname, "Shopname");

                    var InfoMapper = new ModelToTableMapper<Customer>();
                    InfoMapper.AddMapping(c => c.Surname, "Surname");
                    InfoMapper.AddMapping(c => c.Name, "Name");

                    var tblOwner = new SqlTableDependency<Owner>(connectionString, tableName: "Owner", schemaName: "dbo", mapper: InfoMapperOwner, executeUserPermissionCheck: false, includeOldValues: true);
                    var tblCustomer = new SqlTableDependency<Customer>(connectionString, tableName: "Customer", schemaName: "dbo", mapper: InfoMapper, executeUserPermissionCheck: false, includeOldValues: true);

                    tblOwner.OnChanged += (object sender, RecordChangedEventArgs<Owner > e) =>
                    {
                        var changedEntity = e.Entity;
                        webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes($"server says:{DateTime.UtcNow:f} {e.ChangeType} {changedEntity.Shopname}")), result.MessageType, result.EndOfMessage, System.Threading.CancellationToken.None);
                    };

                    tblCustomer.OnChanged += (object sender, RecordChangedEventArgs<Customer> e) =>
                    {
                        webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes($"server says:{DateTime.UtcNow:f} {e.ChangeType} ")), result.MessageType, result.EndOfMessage, System.Threading.CancellationToken.None);
                    };

                    tblCustomer.Start();
                    tblCustomer.Stop();

                    tblOwner.Start();
                    tblOwner.Stop();


                    //using (var tableDependency = new SqlTableDependency<Customer>(connectionString, tableName: "Customer", schemaName: "dbo", mapper: InfoMapper, executeUserPermissionCheck: false, includeOldValues: true))
                    //{
                    //    //tableDependency.OnChanged += TableDependency_Changed(tableDependency);
                    //    tableDependency.OnChanged += (object sender, RecordChangedEventArgs<Customer> e) =>
                    //    {
                    //        webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes($"server says:{DateTime.UtcNow:f} {e.ChangeType} ")), result.MessageType, result.EndOfMessage, System.Threading.CancellationToken.None);
                    //    };
                    //    tableDependency.Start();

                    //    Console.WriteLine("Press a key to exit");
                    //    Console.Read();

                    //    tableDependency.Stop();
                    //}


                    //var startTimeSpan = TimeSpan.Zero;
                    //var periodTimeSpan = TimeSpan.FromSeconds(10);

                    //var timer = new System.Threading.Timer((e) =>
                    //{
                    //webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes($"server says:{DateTime.UtcNow:f} {messages} ")), result.MessageType, result.EndOfMessage, System.Threading.CancellationToken.None);
                    //    }, null, startTimeSpan, periodTimeSpan);

                    // await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes($"server says:{DateTime.UtcNow:f} {messages} ")), result.MessageType, result.EndOfMessage, System.Threading.CancellationToken.None);

                    //Console.WriteLine($"client says:{messages}");
                    //Debug.WriteLine($"client says:{messages}");
                    //await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes($"server says:{ DateTime.UtcNow:f} {messages} ")), result.MessageType, result.EndOfMessage, System.Threading.CancellationToken.None);
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), System.Threading.CancellationToken.None);



                }
            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, System.Threading.CancellationToken.None);

        }


        private static async Task table1(WebSocket webSocket, WebSocketReceiveResult result)
        {
            var buffer = new byte[1024 * 4];
           // WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), System.Threading.CancellationToken.None);

            var connectionString = "Server=localhost;Database=Stock;User Id=newlocaluser;Password=password";

            var InfoMapper = new ModelToTableMapper<Customer>();
            InfoMapper.AddMapping(c => c.Surname, "Surname");
            InfoMapper.AddMapping(c => c.Name, "Name");

            var tableDependency = new SqlTableDependency<Customer>(connectionString, tableName: "Customer", schemaName: "dbo", mapper: InfoMapper, executeUserPermissionCheck: false, includeOldValues: true);
            tableDependency.OnChanged += (object sender, RecordChangedEventArgs<Customer> e) =>
            {
                webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes($"server says:{DateTime.UtcNow:f} {e.ChangeType} ")), result.MessageType, result.EndOfMessage, System.Threading.CancellationToken.None);
            };
            tableDependency.Start();
            Console.WriteLine("Press a key to exit");
            Console.Read();

            tableDependency.Stop();

        }



        private static async Task table2(WebSocket webSocket, WebSocketReceiveResult result)
        {
            var buffer = new byte[1024 * 4];
           // WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), System.Threading.CancellationToken.None);

            var connectionString = "Server=localhost;Database=Stock;User Id=newlocaluser;Password=password";


            var InfoMapperOwner = new ModelToTableMapper<Owner>();
            InfoMapperOwner.AddMapping(c => c.OwnerName, "OwnerName");
            InfoMapperOwner.AddMapping(c => c.Shopname, "Shopname");

            using (var tableDependecy = new SqlTableDependency<Owner>(connectionString, tableName: "Owner", schemaName: "dbo", mapper: InfoMapperOwner, executeUserPermissionCheck: false, includeOldValues: true))
            {
                //tableDependency.OnChanged += TableDependency_Changed(tableDependency);
                tableDependecy.OnChanged += (object sender, RecordChangedEventArgs<Owner> e) =>
                {
                    var changedEntity = e.Entity;
                    webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes($"server says:{DateTime.UtcNow:f} {e.ChangeType} {changedEntity.Shopname}")), result.MessageType, result.EndOfMessage, System.Threading.CancellationToken.None);
                };
                tableDependecy.Start();

                Console.WriteLine("Press a key to exit");
                Console.Read();

                tableDependecy.Stop();
            }
        }

        private void TableDependency_OnChanged(object sender, RecordChangedEventArgs<Customer> e)
        {
            throw new NotImplementedException();
        }

        private static void tst()
        {
            Console.WriteLine("DML operation: ");
        }

        private static void TableDependency_Changed(RecordChangedEventArgs<Customer> e)
        {
            Console.WriteLine(Environment.NewLine);
            if (e.ChangeType != ChangeType.None)
            {
                var changedEntity = e.Entity;
                Console.WriteLine("DML operation: " + e.ChangeType);
                Console.WriteLine("ID: " + changedEntity.Id);
                Console.WriteLine("Name: " + changedEntity.Name);
                Console.WriteLine("Surname: " + changedEntity.Surname);
            }

        }
        public class Customer
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Surname { get; set; }
        }
        public class Owner
        {
            public int Id { get; set; }
            public string OwnerName { get; set; }
            public string Shopname { get; set; }
        }
    }
}
