namespace NuGet
{
    /// <summary>
    /// Specifies how to select most appropriate package version when updating a package.
    /// </summary>
    public enum PackageUpdateMode
    {
        /// <summary>
        /// Selects the newest (highest) version package available.
        /// </summary>
        Newest,

        /// <summary>
        /// Selects the highest version package with the same Major version as the current package.
        /// According to semantic version guidelines, packages with higher Minor version should be
        /// backwards compatible with older packages of the same Major version.
        /// </summary>
        Minor,
        
        /// <summary>
        /// Selects the highest version package with the same Major.Minor version as the current package.
        /// Considered to be "safe" by semantic version guidelines since packages with the same Major.Minor
        /// version should maintain backwards compatibility.
        /// </summary>
        Safe
    }
}