using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace ObjectIdentity.Tests
{
    /// <summary>
    /// Provides configuration services for tests, including connection strings
    /// </summary>
    public static class TestConfig
    {
        private static readonly Lazy<IConfiguration> _configuration = new Lazy<IConfiguration>(() =>
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
        });

        /// <summary>
        /// Gets the test database connection string, using environment variables if available (for CI/CD)
        /// or falling back to appsettings.json for local development
        /// </summary>
        public static string GetTestDbConnectionString()
        {
            // First check if we have the connection string from environment variables (CI environment)
            var envConnString = Environment.GetEnvironmentVariable("ConnectionStrings__testdb");
            if (!string.IsNullOrEmpty(envConnString))
            {
                return envConnString;
            }
            
            // Next try to get it from config (local environment)
            var connString = _configuration.Value.GetConnectionString("testdb");
            if (!string.IsNullOrEmpty(connString))
            {
                return connString;
            }
            
            // Default fallback for local development
            return "Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;Database=SequentialIdTests;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
        }

        /// <summary>
        /// Gets a configuration instance that can be used in tests
        /// </summary>
        public static IConfiguration Configuration => _configuration.Value;
    }
}
