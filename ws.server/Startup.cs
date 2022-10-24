using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

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
                    string msg = Encoding.UTF8.GetString(new ArraySegment<byte>(buffer, 0, result.Count));
                    messages = messages + $" {msg}";

                    var startTimeSpan = TimeSpan.Zero;
                    var periodTimeSpan = TimeSpan.FromSeconds(10);

                    var timer = new System.Threading.Timer((e) =>
                    {
                       webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes($"server says:{DateTime.UtcNow:f} {messages} ")), result.MessageType, result.EndOfMessage, System.Threading.CancellationToken.None);
                    }, null, startTimeSpan, periodTimeSpan);

                   // await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes($"server says:{DateTime.UtcNow:f} {messages} ")), result.MessageType, result.EndOfMessage, System.Threading.CancellationToken.None);

                    //Console.WriteLine($"client says:{messages}");
                    //Debug.WriteLine($"client says:{messages}");
                    //await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes($"server says:{ DateTime.UtcNow:f} {messages} ")), result.MessageType, result.EndOfMessage, System.Threading.CancellationToken.None);
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), System.Threading.CancellationToken.None);



                }
            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, System.Threading.CancellationToken.None);

        }
    }
}
