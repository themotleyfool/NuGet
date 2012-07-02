NuGet.Server + Lucene.Net.Linq
=====

This is a fork of the [NuGet codebase](https://git01.codeplex.com/nuget) on CodePlex that
improves the NuGet web server by integrating the package feed with Lucene.Net.

This fork adds the following features not currently in origin/master:

* Dramatically improve performance of all queries
* Better search using English-language Porter Stem algorithm
* Keeps track of DownloadCount and VersionDownloadCount
* Keeps track of IsLatestVersion flag

Upcoming Features
====

This fork is ready for production, but some additional features may
be implemented next to build upon it:

* Better web-based search interface similar to [NuGet Gallery](https://github.com/NuGet/NuGetGallery)
* Admin interface for managing Lucene index:
    * Rebuild index
	* Delete packages
	* Synchronize index with file system
