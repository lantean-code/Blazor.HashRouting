using System.Text;

namespace Blazor.HashRouting
{
    internal static class HashRoutingUriHelper
    {
        public static string NormalizeHashPrefix(string? hashPrefix)
        {
            var value = string.IsNullOrWhiteSpace(hashPrefix) ? "/" : hashPrefix.Trim();
            if (value[0] != '/')
            {
                value = "/" + value;
            }

            if (value.Length > 1 && value[^1] != '/')
            {
                value += "/";
            }

            return value;
        }

        public static string ToPathAbsoluteUri(string absoluteUri, string baseUri, string hashPrefix)
        {
            ArgumentNullException.ThrowIfNull(absoluteUri);
            ArgumentNullException.ThrowIfNull(baseUri);

            var absolute = new Uri(absoluteUri, UriKind.Absolute);
            var baseAbsolute = new Uri(baseUri, UriKind.Absolute);
            var prefix = NormalizeHashPrefix(hashPrefix);
            var hashRoutePathAndQuery = GetPathAndQueryFromHash(absolute.Fragment, prefix);

            if (hashRoutePathAndQuery is null)
            {
                return BuildAbsoluteUri(absolute, absolute.PathAndQuery);
            }

            var pathAndQuery = CombineBasePathAndRoute(baseAbsolute.AbsolutePath, hashRoutePathAndQuery);

            return BuildAbsoluteUri(baseAbsolute, pathAndQuery);
        }

        public static string ToHashAbsoluteUri(string absolutePathUri, string baseUri, string hashPrefix)
        {
            ArgumentNullException.ThrowIfNull(absolutePathUri);
            ArgumentNullException.ThrowIfNull(baseUri);

            var absolute = new Uri(absolutePathUri, UriKind.Absolute);
            var baseAbsolute = new Uri(baseUri, UriKind.Absolute);
            var prefix = NormalizeHashPrefix(hashPrefix);
            var pathAndQuery = GetRoutePathAndQueryRelativeToBase(absolute, baseAbsolute);
            var relativePathAndQuery = pathAndQuery.Length > 1
                ? pathAndQuery[1..]
                : string.Empty;

            var fragment = prefix + relativePathAndQuery;
            var pathAndFragment = baseAbsolute.AbsolutePath + "#" + fragment;

            return BuildAbsoluteUri(baseAbsolute, pathAndFragment);
        }

        private static string CombineBasePathAndRoute(string basePath, string routePathAndQuery)
        {
            var queryIndex = routePathAndQuery.IndexOf('?');
            var routePath = queryIndex >= 0
                ? routePathAndQuery[..queryIndex]
                : routePathAndQuery;
            var query = queryIndex >= 0
                ? routePathAndQuery[queryIndex..]
                : string.Empty;

            var normalizedBasePath = EnsureBasePath(basePath);
            var combinedPath = routePath == "/"
                ? normalizedBasePath
                : normalizedBasePath + routePath[1..];

            return combinedPath + query;
        }

        private static string GetRoutePathAndQueryRelativeToBase(Uri absolute, Uri baseAbsolute)
        {
            var absolutePath = absolute.AbsolutePath;
            var basePathWithoutTrailingSlash = EnsureBasePathWithoutTrailingSlash(baseAbsolute.AbsolutePath);
            var basePathPrefix = basePathWithoutTrailingSlash + "/";

            string routePath;
            if (string.Equals(absolutePath, basePathWithoutTrailingSlash, StringComparison.Ordinal))
            {
                routePath = "/";
            }
            else if (absolutePath.StartsWith(basePathPrefix, StringComparison.Ordinal))
            {
                routePath = absolutePath[basePathWithoutTrailingSlash.Length..];
            }
            else
            {
                routePath = absolutePath;
            }

            return routePath + absolute.Query;
        }

        private static string? GetPathAndQueryFromHash(string fragment, string hashPrefix)
        {
            if (string.IsNullOrEmpty(fragment) || fragment[0] != '#')
            {
                return null;
            }

            var value = fragment[1..];
            if (!value.StartsWith(hashPrefix, StringComparison.Ordinal))
            {
                return null;
            }

            var remainder = value[hashPrefix.Length..];
            if (string.IsNullOrEmpty(remainder))
            {
                return "/";
            }

            if (remainder[0] == '/')
            {
                return remainder;
            }

            return "/" + remainder;
        }

        private static string EnsureBasePath(string basePath)
        {
            var value = basePath;

            if (value[^1] != '/')
            {
                value += "/";
            }

            return value;
        }

        private static string EnsureBasePathWithoutTrailingSlash(string basePath)
        {
            var value = basePath;

            if (value.Length > 1 && value[^1] == '/')
            {
                value = value[..^1];
            }

            return value;
        }

        private static string BuildAbsoluteUri(Uri absoluteUri, string pathAndQuery)
        {
            var builder = new StringBuilder();
            builder.Append(absoluteUri.Scheme);
            builder.Append("://");
            builder.Append(absoluteUri.Authority);
            builder.Append(pathAndQuery);

            return builder.ToString();
        }
    }
}
