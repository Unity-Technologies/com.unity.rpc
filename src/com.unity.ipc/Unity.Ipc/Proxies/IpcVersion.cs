using System;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using MessagePack;

namespace Unity.Ipc
{
    [MessagePackObject(true)]
    [Serializable]
    public struct IpcVersion : IComparable<IpcVersion>
    {
        private const string versionRegex = @"^(?<major>\d+)(\.?(?<minor>[^.]+))?(\.?(?<patch>[^.]+))?(\.?(?<build>.+))?";
        private const int PART_COUNT = 4;
        public static IpcVersion Default { get; } = default;

        [IgnoreDataMember] private int major;
        [IgnoreDataMember] public int Major { get { Initialize(Version); return major; } }
        [IgnoreDataMember] private int minor;
        [IgnoreDataMember] public int Minor { get { Initialize(Version); return minor; } }
        [IgnoreDataMember] private int patch;
        [IgnoreDataMember] public int Patch { get { Initialize(Version); return patch; } }
        [IgnoreDataMember] private int build;
        [IgnoreDataMember] public int Build { get { Initialize(Version); return build; } }
        [IgnoreDataMember] private string special;
        [IgnoreDataMember] public string Special { get { Initialize(Version); return special; } }
        [IgnoreDataMember] private bool isAlpha;
        [IgnoreDataMember] public bool IsAlpha { get { Initialize(Version); return isAlpha; } }
        [IgnoreDataMember] private bool isBeta;
        [IgnoreDataMember] public bool IsBeta { get { Initialize(Version); return isBeta; } }
        [IgnoreDataMember] private bool isUnstable;
        [IgnoreDataMember] public bool IsUnstable { get { Initialize(Version); return isUnstable; } }

        [IgnoreDataMember] private int[] intParts;
        [IgnoreDataMember] private string[] stringParts;
        [IgnoreDataMember] private int parts;
        [IgnoreDataMember] private bool initialized;
        [IgnoreDataMember] private string version;

        public string Version { get => version ?? (version = string.Empty); set => version = value; }

        private static readonly Regex regex = new Regex(versionRegex);

        public static IpcVersion Parse(string version)
        {
            return default(IpcVersion).Initialize(version);
        }

        private IpcVersion Initialize(string theVersion)
        {
            if (initialized)
                return this;

            this.Version = theVersion?.Trim() ?? String.Empty;

            isAlpha = false;
            isBeta = false;
            major = 0;
            minor = 0;
            patch = 0;
            build = 0;
            special = null;
            parts = 0;

            intParts = new int[PART_COUNT];
            stringParts = new string[PART_COUNT];
            for (var i = 0; i < PART_COUNT; i++)
                stringParts[i] = intParts[i].ToString();

            if (String.IsNullOrEmpty(theVersion))
                return this;

            var match = regex.Match(theVersion);
            if (!match.Success)
            {
                return this;
            }

            major = int.Parse(match.Groups["major"].Value);
            intParts[parts] = major;
            stringParts[parts] = major.ToString();
            parts = 1;

            var minorMatch = match.Groups["minor"];
            var patchMatch = match.Groups["patch"];
            var buildMatch = match.Groups["build"];

            if (minorMatch.Success)
            {
                if (!int.TryParse(minorMatch.Value, out minor))
                {
                    special = minorMatch.Value.TrimEnd();
                    stringParts[parts] = special ?? "0";
                }
                else
                {
                    intParts[parts] = minor;
                    stringParts[parts] = minor.ToString();
                    parts++;

                    if (patchMatch.Success)
                    {
                        if (!int.TryParse(patchMatch.Value, out patch))
                        {
                            special = patchMatch.Value.TrimEnd();
                            stringParts[parts] = special ?? "0";
                        }
                        else
                        {
                            intParts[parts] = patch;
                            stringParts[parts] = patch.ToString();
                            parts++;

                            if (buildMatch.Success)
                            {
                                if (!int.TryParse(buildMatch.Value, out build))
                                {
                                    special = buildMatch.Value.TrimEnd();
                                    stringParts[parts] = special ?? "0";
                                }
                                else
                                {
                                    intParts[parts] = build;
                                    stringParts[parts] = build.ToString();
                                    parts++;
                                }
                            }
                        }
                    }
                }
            }

            isUnstable = special != null;
            if (isUnstable)
            {
                isAlpha = special.IndexOf("alpha") >= 0;
                isBeta = special.IndexOf("beta") >= 0;
            }
            initialized = true;
            return this;
        }

        public override string ToString()
        {
            return Version;
        }

        public int CompareTo(IpcVersion other)
        {
            if (this > other)
                return 1;
            if (this == other)
                return 0;
            return -1;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 23 + Major.GetHashCode();
            hash = hash * 23 + Minor.GetHashCode();
            hash = hash * 23 + Patch.GetHashCode();
            hash = hash * 23 + Build.GetHashCode();
            hash = hash * 23 + (Special != null ? Special.GetHashCode() : 0);
            return hash;
        }

        public override bool Equals(object obj)
        {
            if (obj is IpcVersion ipcVersion)
                return Equals(ipcVersion);
            return false;
        }

        public bool Equals(IpcVersion other)
        {
            return this == other;
        }

        public static bool operator ==(IpcVersion lhs, IpcVersion rhs)
        {
            if (lhs.Version == rhs.Version)
                return true;
            return
                (lhs.Major == rhs.Major) &&
                    (lhs.Minor == rhs.Minor) &&
                    (lhs.Patch == rhs.Patch) &&
                    (lhs.Build == rhs.Build) &&
                    (lhs.Special == rhs.Special);
        }

        public static bool operator !=(IpcVersion lhs, IpcVersion rhs)
        {
            return !(lhs == rhs);
        }

        public static bool operator >(IpcVersion lhs, IpcVersion rhs)
        {
            if (lhs.Version == rhs.Version)
                return false;
            if (!lhs.initialized)
                return false;
            if (!rhs.initialized)
                return true;

            for (var i = 0; i < lhs.parts && i < rhs.parts; i++)
            {
                if (lhs.intParts[i] != rhs.intParts[i])
                    return lhs.intParts[i] > rhs.intParts[i];
            }

            for (var i = 1; i < PART_COUNT; i++)
            {
                var ret = CompareVersionStrings(lhs.stringParts[i], rhs.stringParts[i]);
                if (ret != 0)
                    return ret > 0;
            }

            return false;
        }

        public static bool operator <(IpcVersion lhs, IpcVersion rhs)
        {
            return !(lhs > rhs);
        }

        public static bool operator >=(IpcVersion lhs, IpcVersion rhs)
        {
            return lhs > rhs || lhs == rhs;
        }

        public static bool operator <=(IpcVersion lhs, IpcVersion rhs)
        {
            return lhs < rhs || lhs == rhs;
        }

        private static int CompareVersionStrings(string lhs, string rhs)
        {
            int lhsNumber = GetNumberFromVersionString(lhs, out int lhsNonDigitPos);
            int rhsNumber = GetNumberFromVersionString(rhs, out int rhsNonDigitPos);

            if (lhsNumber != rhsNumber)
                return lhsNumber.CompareTo(rhsNumber);

            if (lhsNonDigitPos < 0 && rhsNonDigitPos < 0)
                return 0;

            // versions with alphanumeric characters are always lower than ones without
            // i.e. 1.1alpha is lower than 1.1
            if (lhsNonDigitPos < 0)
                return 1;
            if (rhsNonDigitPos < 0)
                return -1;
            return string.Compare(lhs.Substring(lhsNonDigitPos), rhs.Substring(rhsNonDigitPos), StringComparison.Ordinal);
        }

        private static int GetNumberFromVersionString(string lhs, out int nonDigitPos)
        {
            nonDigitPos = IndexOfFirstNonDigit(lhs);
            int number = -1;
            if (nonDigitPos > -1)
            {
                int.TryParse(lhs.Substring(0, nonDigitPos), out number);
            }
            else
            {
                int.TryParse(lhs, out number);
            }
            return number;
        }

        private static int IndexOfFirstNonDigit(string str)
        {
            for (var i = 0; i < str.Length; i++)
            {
                if (!char.IsDigit(str[i]))
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
