using System;
using System.IO;
using System.Threading.Tasks;
using CodeBuddy.Core.Implementation;
using CodeBuddy.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodeBuddy.E2ETests.Infrastructure
{
    public abstract class E2ETestBase : IAsyncLifetime
    {
        protected IServiceProvider ServiceProvider { get; private set; }
        protected string TestWorkspace { get; private set; }

        public virtual async Task InitializeAsync()
        {
            // Set up test workspace
            TestWorkspace = Path.Combine(Path.GetTempPath(), "CodeBuddy_E2E_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(TestWorkspace);

            // Configure services
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            await OnTestInitializeAsync();
        }

        public virtual async Task DisposeAsync()
        {
            // Cleanup
            if (Directory.Exists(TestWorkspace))
            {
                Directory.Delete(TestWorkspace, true);
            }

            await OnTestDisposeAsync();
        }

        protected virtual void ConfigureServices(IServiceCollection services)
        {
            // Register core services
            services.AddSingleton<IConfigurationManager, ConfigurationManager>();
            services.AddSingleton<IPluginManager, PluginManager>();
            services.AddSingleton<IFileOperations, FileOperations>();
            services.AddSingleton<ITemplateManager, TemplateManager>();
            services.AddSingleton<ICodeGenerator, CodeGenerator>();
            services.AddSingleton<IRuleManager, RuleManager>();
        }

        protected virtual Task OnTestInitializeAsync() => Task.CompletedTask;
        protected virtual Task OnTestDisposeAsync() => Task.CompletedTask;

        protected string GetTestFilePath(string fileName)
        {
            return Path.Combine(TestWorkspace, fileName);
        }

        protected void CopyTestData(string sourcePath, string targetFileName)
        {
            var targetPath = GetTestFilePath(targetFileName);
            File.Copy(sourcePath, targetPath, true);
        }
    }
}