using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolutionCleanup;

internal class DirectoryInfoComparer : IEqualityComparer<DirectoryInfo>
{
    public bool Equals(DirectoryInfo x, DirectoryInfo y) => string.Equals(x?.FullName, y?.FullName);

    public int GetHashCode(DirectoryInfo obj) => obj.FullName.GetHashCode();
}
