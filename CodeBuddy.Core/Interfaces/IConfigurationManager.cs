using System;
using System.Threading.Tasks;
using CodeBuddy.Core.Models.Configuration;

namespace CodeBuddy.Core.Interfaces
{
    /// <summary>
    /// Interface for managing application configuration
    /// </summary>
    public interface IConfigurationManager
    {
        /// <summary>
        /// Gets a configuration section
        /// </summary>
        /// <typeparam name="T">Configuration type</typeparam>
        /// <param name="section">Configuration section name</param>
        /// <returns>Configuration instance</returns>
        Task<T> GetConfigurationAsync<T>(string section) where T : BaseConfiguration, new();

        /// <summary>
        /// Saves a configuration section
        /// </summary>
        /// <typeparam name="T">Configuration type</typeparam>
        /// <param name="section">Configuration section name</param>
        /// <param name="configuration">Configuration instance to save</param>
        Task SaveConfigurationAsync<T>(string section, T configuration) where T : BaseConfiguration;

        /// <summary>
        /// Registers a callback to be invoked when configuration changes
        /// </summary>
        /// <typeparam name="T">Configuration type</typeparam>
        /// <param name="section">Configuration section name</param>
        /// <param name="callback">Callback to invoke</param>
        void RegisterConfigurationChangeCallback<T>(string section, Action<T> callback) where T : BaseConfiguration;
    }
}