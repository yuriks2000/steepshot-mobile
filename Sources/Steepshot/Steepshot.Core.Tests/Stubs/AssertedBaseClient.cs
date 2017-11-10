﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Steepshot.Core.HttpClient;
using Steepshot.Core.Models.Common;

namespace Steepshot.Core.Tests.Stubs
{
    public class AssertedBaseClient : BaseClient
    {
        public AssertedBaseClient(string url)
        {
            Gateway = new ApiGateway(url);
            EnableRead = true;
        }

        protected override OperationResult<T> CreateResult<T>(string json, OperationResult error)
        {
            if (error.Success)
            {
                var jObject = JsonConverter.Deserialize<JObject>(json);
                var type = typeof(T);
                var mNames = GetPropertyNames(type);
                var jNames = jObject.Children().Select(jtoken => ToTitleCase(jtoken.Path)).ToArray();

                var msg = new List<string>();
                foreach (var pName in jNames)
                {
                    if (!mNames.Contains(pName))
                    {
                        msg.Add($"Missing in model {pName}");
                    }
                }
                foreach (var pName in mNames)
                {
                    if (!jNames.Contains(pName))
                    {
                        msg.Add($"Missing in json {pName}");
                    }
                }

                if (msg.Any())
                {
                    error.Errors.Add($"Some properties ({msg.Count}) was missed! {Environment.NewLine} {string.Join(Environment.NewLine, msg)}");
                }
            }
            return base.CreateResult<T>(json, error);
        }

        private HashSet<string> GetPropertyNames(Type type)
        {
            var props = type.GetRuntimeProperties();
            var resp = new HashSet<string>();
            foreach (var prop in props)
            {
                var order = prop.GetCustomAttribute<JsonPropertyAttribute>();
                if (order != null)
                {
                    resp.Add(order.PropertyName);
                }
                else
                {
                    resp.Add(prop.Name);
                }
            }
            return resp;
        }

        private string ToTitleCase(string name, bool firstUpper = true)
        {
            var sb = new StringBuilder(name);
            for (var i = 0; i < sb.Length; i++)
            {
                if (i == 0 && firstUpper)
                    sb[i] = char.ToUpper(sb[i]);

                if (sb[i] == '_' && i + 1 < sb.Length)
                    sb[i + 1] = char.ToUpper(sb[i + 1]);
            }
            sb.Replace("_", string.Empty);
            var rez = sb.ToString();

            return rez;
        }
    }
}
