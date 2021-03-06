﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Azure.IoTSolutions.ReverseProxy.Exceptions;
using Microsoft.Extensions.Configuration;

// TODO: tests
namespace Microsoft.Azure.IoTSolutions.ReverseProxy.Runtime
{
    public interface IConfigData
    {
        string GetString(string key);
        int GetInt(string key);

        bool GetBool(string key);
    }

    public class ConfigData : IConfigData
    {
        private readonly IConfigurationRoot configuration;

        public ConfigData()
        {
            // More info about configuration at
            // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration

            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddIniFile("appsettings.ini", optional: false, reloadOnChange: false);
            this.configuration = configurationBuilder.Build();
        }

        public string GetString(string key)
        {
            var value = this.configuration.GetValue<string>(key);
            return ReplaceEnvironmentVariables(value);
        }

        public bool GetBool(string key)
        {
            var value = this.configuration.GetValue<string>(key).ToLowerInvariant();
            return value == "true"
                   || value == "t"
                   || value == "yes"
                   || value == "y"
                   || value == "1";
        }

        public int GetInt(string key)
        {
            try
            {
                return Convert.ToInt32(this.GetString(key));
            }
            catch (Exception e)
            {
                throw new InvalidConfigurationException($"Unable to load configuration value for '{key}'", e);
            }
        }

        private static string ReplaceEnvironmentVariables(string value)
        {
            return string.IsNullOrEmpty(value) ? value : ProcessMandatoryPlaceholders(value);
        }

        private static string ProcessMandatoryPlaceholders(string value)
        {
            // Pattern for mandatory replacements: ${VAR_NAME}
            const string pattern = @"\${([a-zA-Z_][a-zA-Z0-9_]*)}";

            // Search
            var keys = (from Match m in Regex.Matches(value, pattern)
                select m.Groups[1].Value).Distinct().ToArray();

            // Replace
            foreach (DictionaryEntry x in Environment.GetEnvironmentVariables())
            {
                if (keys.Contains(x.Key))
                {
                    value = value.Replace("${" + x.Key + "}", x.Value.ToString());
                }
            }

            // Non replaced placeholders cause an exception
            keys = (from Match m in Regex.Matches(value, pattern)
                select m.Groups[1].Value).ToArray();
            if (keys.Length > 0)
            {
                var varsNotFound = keys.Aggregate(", ", (current, k) => current + k);
                throw new InvalidConfigurationException("Environment variables not found: " + varsNotFound);
            }

            return value;
        }
    }
}