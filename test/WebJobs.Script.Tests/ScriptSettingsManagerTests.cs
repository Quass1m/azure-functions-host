﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Script.Config;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Microsoft.Azure.WebJobs.Script.Tests
{
    public class ScriptSettingsManagerTests
    {
        [Theory]
        [InlineData("testsite", "production", "testsite")]
        [InlineData("testsite", "Production", "testsite")]
        [InlineData("testsite", null, "testsite")]
        [InlineData("testsite", "staging", "testsite-staging")]
        [InlineData("testsite", "dev", "testsite-dev")]
        [InlineData("TestSite", "Dev", "testsite-dev")]
        public void UniqueSlotName_ReturnsExpectedValue(string siteName, string slotName, string expectedValue)
        {
            var settingsManager = ScriptSettingsManager.Instance;

            var variables = new Dictionary<string, string>
            {
                { EnvironmentSettingNames.AzureWebsiteName, siteName },
                { EnvironmentSettingNames.AzureWebsiteSlotName, slotName },
            };

            using (var tempVariables = new TestScopedEnvironmentVariable(variables))
            {
                Assert.Equal(expectedValue, ScriptSettingsManager.Instance.AzureWebsiteUniqueSlotName);
            }
        }

        [Fact]
        public void SettingsAreNotCached()
        {
            using (var variable = new TestScopedEnvironmentVariable(nameof(SettingsAreNotCached), "foo"))
            {
                Assert.Equal("foo", ScriptSettingsManager.Instance.GetSetting(nameof(SettingsAreNotCached)));

                Environment.SetEnvironmentVariable(nameof(SettingsAreNotCached), "bar");
                Assert.Equal("bar", ScriptSettingsManager.Instance.GetSetting(nameof(SettingsAreNotCached)));
            }
        }

        [Theory]
        [InlineData("Foo__Bar__Baz", "Foo__Bar__Baz")]
        [InlineData("Foo__Bar__Baz", "foo__bar__baz")]
        [InlineData("Foo__Bar__Baz", "Foo:Bar:Baz")]
        [InlineData("Foo__Bar__Baz", "foo:bar:baz")]
        [InlineData("Foo:Bar:Baz", "Foo:Bar:Baz")]
        [InlineData("Foo:Bar:Baz", "foo:bar:baz")]
        [InlineData("Foo_Bar_Baz", "Foo_Bar_Baz")]
        [InlineData("Foo_Bar_Baz", "foo_bar_baz")]
        [InlineData("FooBarBaz", "FooBarBaz")]
        [InlineData("FooBarBaz", "foobarbaz")]
        public void GetSetting_NormalizesKeys(string key, string lookup)
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && key.IndexOf(":") != -1)
                {
                    // : is not a valid character for Unix env var names
                    return;
                }

                string value = Guid.NewGuid().ToString();
                Environment.SetEnvironmentVariable(key, value);

                string result = ScriptSettingsManager.Instance.GetSetting(lookup);
                Assert.Equal(value, result);
            }
            finally
            {
                Environment.SetEnvironmentVariable(key, null);
            }
        }
    }
}
