NuGet.Server + Lucene.Net.Linq
=====

This is a fork of the [NuGet codebase](https://git01.codeplex.com/nuget) on CodePlex that
improves the NuGet web server by integrating the package feed with Lucene.Net.

This fork adds the following features not currently in origin/master:

* Dramatically improve performance of all queries
* Better search using English-language Porter Stem algorithm
* Keeps track of DownloadCount and VersionDownloadCount
* Keeps track of IsLatestVersion flag

Incompatible Changes
=====

Whereas before packages could be published simply by copying them
into <code>~/Packages/</code>, this fork requires packages to be
published via e.g. <code>nuget push MyPackage.1.0.nupkg -s http://localhost/ my-api-key</code>
or they will not be added to the Lucene index, and therefore they
will not show up in the feed.

Conversely, when removing packages from the feed, this must also be
performed by executing e.g. <code>nuget delete MyPackage 1.0 -Source http://localhost/ -ApiKey my-api-key</code>
in order for them to be removed from the Lucene index.

Upcoming Features
====

This fork is ready for production, but some additional features may
be implemented next to build upon it:

* Better web-based search interface similar to [NuGet Gallery](https://github.com/NuGet/NuGetGallery)
* Admin interface for managing Lucene index:
    * Rebuild index
	* Delete packages
	* Synchronize index with file system
