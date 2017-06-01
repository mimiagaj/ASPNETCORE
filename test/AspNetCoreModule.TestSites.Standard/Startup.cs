// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AspnetCoreModule.TestSites.Standard
{
    public class Startup
    {
        public static class Commands
        {
            public static string command_websocket = "websocket";
            public static string command_websocketSubProtocol = "websocketSubProtocol";
            public static string command_GetProcessId = "GetProcessId";
            public static string command_EchoPostData = "EchoPostData";
            public static string command_contentlength = "contentlength";
            public static string command_connectionclose = "connectionclose";
            public static string command_notchunked = "notchunked";
            public static string command_chunked = "chunked";
            public static string command_manuallychunked = "manuallychunked";
            public static string command_manuallychunkedandclose = "manuallychunkedandclose";
            public static string command_ImpersonateMiddleware = "ImpersonateMiddleware";
            public static string command_DoSleep = "DoSleep";
            public static string command_MemoryLeak = "MemoryLeak";
            public static string command_ExpandEnvironmentVariables = "ExpandEnvironmentVariables";
            public static string command_GetEnvironmentVariables = "GetEnvironmentVariables";
            public static string command_DumpEnvironmentVariables = "DumpEnvironmentVariables";
            public static string command_GetRequestHeaderValue = "GetRequestHeaderValue";
            public static string command_DumpRequestHeaders = "DumpRequestHeaders";
            public static string command_ANCMTestEnvStartUpDelay = "ANCMTestEnvStartUpDelay";
            public static string command_ANCMTestEnvShutdownDelay = "ANCMTestEnvShutdownDelay";

            public static string[] commands = new string[] {
                command_websocket,
                command_websocketSubProtocol,
                command_GetProcessId,
                command_EchoPostData,
                command_contentlength,
                command_connectionclose,
                command_notchunked,
                command_chunked,
                command_manuallychunked,
                command_manuallychunkedandclose,
                command_ImpersonateMiddleware,
                command_DoSleep,
                command_MemoryLeak,
                command_ExpandEnvironmentVariables,
                command_GetEnvironmentVariables,
                command_DumpEnvironmentVariables,
                command_GetRequestHeaderValue,
                command_DumpRequestHeaders,
                command_ANCMTestEnvStartUpDelay,
                command_ANCMTestEnvShutdownDelay
            };
        }

        public static int SleeptimeWhileClosing = 0;
        public static int SleeptimeWhileStarting = 0;
        public static List<byte[]> MemoryLeakList = new List<byte[]>();
        

        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<IISOptions>(options => {
                // Considering the default value of ForwardWindowsAuthentication is true,
                // the below line is not required at present, however keeping in case the default value is changed later.
                options.ForwardWindowsAuthentication = true; 
            });
        } 

        private async Task Echo(WebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            while (!result.CloseStatus.HasValue)
            {
                await webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None);
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            }
            await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
        }
        
        public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(minLevel: LogLevel.Warning);

            app.Map("/help", subApp =>
            {
                subApp.Run(async context =>
                {
                    string helpContent = string.Empty;
                    foreach (var item in Commands.commands)
                    {
                        helpContent += item + "<br>";
                    }
                    await context.Response.WriteAsync(helpContent);
                });
            });

            app.Map("/" + Commands.command_websocketSubProtocol, subApp =>
            {
                app.UseWebSockets(new WebSocketOptions
                {
                    ReplaceFeature = true
                });

                subApp.Use(async (context, next) =>
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        var webSocket = await context.WebSockets.AcceptWebSocketAsync("mywebsocketsubprotocol");
                        await Echo(webSocket);                                                
                    }
                    else
                    {
                        var wsScheme = context.Request.IsHttps ? "wss" : "ws";
                        var wsUrl = $"{wsScheme}://{context.Request.Host.Host}:{context.Request.Host.Port}{context.Request.Path}";
                        await context.Response.WriteAsync($"Ready to accept a WebSocket request at: {wsUrl}");
                    }
                });
            });

            app.Map("/" + Commands.command_websocket, subApp =>
            {
                app.UseWebSockets(new WebSocketOptions
                {
                    ReplaceFeature = true
                });

                subApp.Use(async (context, next) =>
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        var webSocket = await context.WebSockets.AcceptWebSocketAsync("");
                        await Echo(webSocket);
                    }
                    else
                    {
                        var wsScheme = context.Request.IsHttps ? "wss" : "ws";
                        var wsUrl = $"{wsScheme}://{context.Request.Host.Host}:{context.Request.Host.Port}{context.Request.Path}";
                        await context.Response.WriteAsync($"Ready to accept a WebSocket request at: {wsUrl}");
                    }
                });
            });

            app.Map("/" + Commands.command_GetProcessId, subApp =>
            {
                subApp.Run(context =>
                {
                    var process = Process.GetCurrentProcess();
                    return context.Response.WriteAsync(process.Id.ToString());
                });
            });
            
            app.Map("/" + Commands.command_EchoPostData, subApp =>
            {
                subApp.Run(context =>
                {
                    string responseBody = string.Empty;
                    if (string.Equals(context.Request.Method, "POST", StringComparison.OrdinalIgnoreCase))
                    {
                        var form = context.Request.ReadFormAsync().GetAwaiter().GetResult();
                        int counter = 0;
                        foreach (var key in form.Keys)
                        {
                            StringValues output;
                            if (form.TryGetValue(key, out output))
                            {
                                responseBody += key + "=";
                                foreach (var line in output)
                                {
                                    responseBody += line;
                                }
                                if (++counter < form.Count)
                                {
                                    responseBody += "&";
                                }
                            }
                        }
                    }
                    else
                    {
                        responseBody = "NoAction";
                    }
                    return context.Response.WriteAsync(responseBody);
                });
            });

            app.Map("/" + Commands.command_contentlength, subApp =>
            {
                subApp.Run(context =>
                {
                    context.Response.ContentLength = 14;
                    return context.Response.WriteAsync("Content Length");
                });
            });

            app.Map("/" + Commands.command_connectionclose, subApp =>
            {
                subApp.Run(async context =>
                {
                    context.Response.Headers[HeaderNames.Connection] = "close";
                    await context.Response.WriteAsync("Connnection Close");
                    await context.Response.Body.FlushAsync(); // Bypass IIS write-behind buffering
                });
            });

            app.Map("/" + Commands.command_notchunked, subApp =>
            {
                subApp.Run(async context =>
                {
                    var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                    //context.Response.ContentLength = encoding.GetByteCount(document);
                    context.Response.ContentType = "text/html;charset=UTF-8";                    
                    await context.Response.WriteAsync("NotChunked", encoding, context.RequestAborted);
                    await context.Response.Body.FlushAsync(); // Bypass IIS write-behind buffering
                });
            });

            app.Map("/" + Commands.command_chunked, subApp =>
            {
                subApp.Run(async context =>
                {
                    await context.Response.WriteAsync("Chunked");
                    await context.Response.Body.FlushAsync(); // Bypass IIS write-behind buffering
                });
            });
                        
            app.Map("/" + Commands.command_manuallychunked, subApp =>
            {
                subApp.Run(context =>
                {
                    context.Response.Headers[HeaderNames.TransferEncoding] = "chunked";
                    return context.Response.WriteAsync("10\r\nManually Chunked\r\n0\r\n\r\n");
                });
            });

            app.Map("/" + Commands.command_manuallychunkedandclose, subApp =>
            {
                subApp.Run(context =>
                {
                    context.Response.Headers[HeaderNames.Connection] = "close";
                    context.Response.Headers[HeaderNames.TransferEncoding] = "chunked";
                    return context.Response.WriteAsync("1A\r\nManually Chunked and Close\r\n0\r\n\r\n");
                });
            });

            app.Map("/" + Commands.command_ImpersonateMiddleware, subApp =>
            {
                subApp.UseMiddleware<ImpersonateMiddleware>();                
                subApp.Run(context =>
                {
                     return context.Response.WriteAsync("");
                });
            });

            app.Run(context =>
            {
                string response = "Running";
                string[] paths = context.Request.Path.Value.Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string item in paths)
                {
                    string action = string.Empty;
                    string parameter = string.Empty;

                    action = Commands.command_DoSleep;
                    if (item.StartsWith(action))
                    {
                        /* 
                          Process "DoSleep" command here.
                          For example, if path contains "DoSleep" such as /DoSleep1000, run Thread.Sleep(1000)
                        */
                        int sleepTime = 1000;
                        if (item.Length > action.Length)
                        {
                            parameter = item.Substring(action.Length);
                            sleepTime = Convert.ToInt32(parameter);
                        }
                        Thread.Sleep(sleepTime);
                    }

                    action = Commands.command_MemoryLeak;
                    if (item.StartsWith(action))
                    {
                        parameter = "1024";
                        if (item.Length > action.Length)
                        {
                            parameter = item.Substring(action.Length);                            
                        }
                        long size = Convert.ToInt32(parameter);                        
                        var rnd = new Random();                        
                        byte[] b = new byte[size*1024];
                        b[rnd.Next(0, b.Length)] = byte.MaxValue;
                        MemoryLeakList.Add(b); 
                        response = "MemoryLeak, size:" + size.ToString() + " KB, total: " + MemoryLeakList.Count.ToString();
                    }
                    
                    action = Commands.command_ExpandEnvironmentVariables;
                    if (item.StartsWith(action))
                    {
                        if (item.Length > action.Length)
                        {
                            parameter = item.Substring(action.Length);
                            response = Environment.ExpandEnvironmentVariables("%" + parameter + "%");
                        }                        
                    }

                    action = Commands.command_GetEnvironmentVariables;
                    if (item.StartsWith(action))
                    {
                        parameter = item.Substring(action.Length);
                        response = Environment.GetEnvironmentVariables().Count.ToString();
                    }

                    action = Commands.command_DumpEnvironmentVariables;
                    if (item.StartsWith(action))
                    {
                        response = String.Empty;

                        foreach (DictionaryEntry de in Environment.GetEnvironmentVariables())
                        { 
                            response += de.Key + ":" + de.Value + "<br/>";
                        }                        
                    }

                    action = Commands.command_GetRequestHeaderValue;
                    if (item.StartsWith(action))
                    {
                        if (item.Length > action.Length)
                        {
                            parameter = item.Substring(action.Length);

                            if (context.Request.Headers.ContainsKey(parameter))
                            {
                                response = context.Request.Headers[parameter];
                            }
                            else
                            {
                                response = "";
                            }
                        }
                    }

                    action = Commands.command_DumpRequestHeaders;
                    if (item.StartsWith(action))
                    {
                        response = String.Empty;
                        
                        foreach (var de in context.Request.Headers)
                        {
                            response += de.Key + ":" + de.Value + "<br/>";
                        }
                    }                    
                }
                return context.Response.WriteAsync(response);
            });
        }
    }
}
