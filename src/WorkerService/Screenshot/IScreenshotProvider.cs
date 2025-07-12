namespace WorkerService.Screenshot
{
    /// <summary>
    /// Interface for different screenshot capture methods
    /// </summary>
    public interface IScreenshotProvider
    {
        /// <summary>
        /// Takes a screenshot and returns the image data as byte array
        /// </summary>
        /// <returns>Screenshot image data in PNG format, or null if capture failed</returns>
        byte[]? TakeScreenshot();

        /// <summary>
        /// Name of the screenshot provider implementation
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Checks if this provider is available on the current system
        /// </summary>
        /// <returns>True if the provider can be used, false otherwise</returns>
        bool IsAvailable();
    }
}

