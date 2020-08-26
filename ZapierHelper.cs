﻿using CMS.Core;
using CMS.DataEngine;
using CMS.EventLog;
using CMS.Helpers;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Xperience.Zapier
{
    public class ZapierHelper
    {
        private static readonly HttpClient client = new HttpClient();
        protected static List<WebhookHandler> mHandlers = new List<WebhookHandler>();

        public static void RegisterWebhook(int id, bool logTask = false)
        {
            var webhook = WebhookInfoProvider.GetWebhookInfo(id);
            if (webhook != null) RegisterWebhook(webhook, logTask);
        }

        public static void RegisterWebhook(WebhookInfo webhook, bool logTask = false)
        {
            var handler = new WebhookHandler(webhook);
            mHandlers.Add(handler);
            if (webhook.WebhookEnabled)
            {
                if(handler.Register())
                {
                    // Create web farm task
                    if(logTask)
                    {
                        var webFarmService = Service.Resolve<IWebFarmService>();
                        webFarmService.CreateTask(new RegisterWebhookWebFarmTask()
                        {
                            WebhookID = webhook.WebhookID
                        });
                    }
                }
            }
        }

        public static void UnregisterWebhook(int id, bool logTask = false)
        {
            var webhook = WebhookInfoProvider.GetWebhookInfo(id);
            if (webhook != null) UnregisterWebhook(webhook, logTask);
        }

        public static void UnregisterWebhook(WebhookInfo webhook, bool logTask = false)
        {
            var handler = mHandlers.Where(h => h.Webhook.WebhookID == webhook.WebhookID).FirstOrDefault();
            if (handler != null)
            {
                if(handler.Unregister())
                {
                    mHandlers.Remove(handler);

                    // Create web farm task
                    if(logTask)
                    {
                        var webFarmService = Service.Resolve<IWebFarmService>();
                        webFarmService.CreateTask(new UnregisterWebhookWebFarmTask()
                        {
                            WebhookID = webhook.WebhookID
                        });
                    }
                }
            }
        }

        public static WebhookEventTypeEnum GetWebhookEventTypeEnum(int value)
        {
            switch(value){
                case 0:
                    return WebhookEventTypeEnum.Create;
                case 1:
                    return WebhookEventTypeEnum.Update;
                case 2:
                    return WebhookEventTypeEnum.Delete;
            }

            return WebhookEventTypeEnum.None;
        }

        public static bool SendPostToWebhook(string url, BaseInfo data)
        {
            return DoPost(url, data.ToZapierString());
        }

        public static bool SendPostToWebhook(string url, IEnumerable<BaseInfo> data)
        {
            var content = data.Select(b => b.ToZapierObject());
            var json = JsonConvert.SerializeObject(content);

            return DoPost(url, json);
        }

        private static bool DoPost(string url, string content)
        {
            if (DataHelper.IsEmpty(url))
            {
                return false;
            }

            var buffer = System.Text.Encoding.UTF8.GetBytes(content);
            var byteContent = new ByteArrayContent(buffer);
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var response = client.PostAsync(url, byteContent).Result;
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                var message = response.Content.ReadAsStringAsync().Result;
                EventLogProvider.LogEvent("I", nameof(ZapierHelper), "POST", $"POST to {url} failed with the following message:<br/> {message}");
                return false;
            }

            return true;
        }
    }
}