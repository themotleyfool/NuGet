<%@ Page Language="C#" %>
<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">

<html xmlns="http://www.w3.org/1999/xhtml">
<head id="Head1" runat="server">
    <title>NuGet Private Repository</title>
    <style>
        body { font-family: Calibri; width: 800px;}
        legend { font-weight: bold; }
        img {  float: right; }
    </style>
</head>
<body>
    <h2>You are running NuGet.Server v<%= typeof(NuGet.Server.DataServices.Package).Assembly.GetName().Version %></h2>
    <p>
        Click <a href="<%= VirtualPathUtility.ToAbsolute("~/api/v2/Packages") %>">here</a> to view your packages.
    </p>
    <fieldset>
        <legend>Repository URLs</legend>
        In the package manager settings, add the following URL to the list of 
        Package Sources:
        <blockquote>
            <strong><%= Helpers.GetRepositoryUrl(Request.Url, Request.ApplicationPath) %></strong>
        </blockquote>
        <% if (String.IsNullOrEmpty(ConfigurationManager.AppSettings["apiKey"])) { %>
        To enable pushing packages to this feed using the nuget command line tool (nuget.exe). Set the api key appSetting in web.config.
        <% } %> 
        <% else { %>
        Use the command below to push packages to this feed using the nuget command line tool (nuget.exe).
        <% } %>
        <blockquote>
            <strong>nuget push {package file} -s <%= Helpers.GetPushUrl(Request.Url, Request.ApplicationPath) %> {apikey}</strong>
        </blockquote>            
    </fieldset>
		
    <fieldset>
        <legend>Search</legend>
        <asp:Image runat="server" ImageUrl="~/img/lucene-net-badge-180x36.png" AlternateText="Powered by Lucene.Net"/>
		<form action="<%= VirtualPathUtility.ToAbsolute("~/search")%>" method="get">
            <select name="includePrerelease">
                <option value="false">Stable Only</option>
                <option value="true">Include Prerelease</option>
            </select>

			<input type="text" name="query" value=""/>
			<input type="hidden" name="pageSize" value="10"/>

			<input type="submit" value="Search"/>
		</form>
    </fieldset>
</body>
</html>
