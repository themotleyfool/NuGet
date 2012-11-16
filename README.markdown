NuGet.Server + Lucene.Net.Linq
=====

This is a fork of the [NuGet codebase](https://git01.codeplex.com/nuget) on CodePlex that
improves the NuGet web server by integrating the package feed with Lucene.Net.

This fork adds the following features not currently in origin/master:

* Dramatically improve performance of all queries
* Better search using English-language Porter Stem algorithm
* Keeps track of DownloadCount and VersionDownloadCount
* Keeps track of IsLatestVersion flag
