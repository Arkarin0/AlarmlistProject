// Created/modified by Arkarin0 under one ore more license(s).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Alarmlist.Output
{
    /// <summary>Explicit entry type and captured assemblies. Loading never reads their paths.</summary>
    public sealed class ExportProviderReference
    {
        public ExportProviderReference(string id, string filePath, string typeName, IEnumerable<ExportAssemblyImage> images)
        {
            ValidateId(id);
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("A provider assembly path is required.", nameof(filePath));
            if (string.IsNullOrWhiteSpace(typeName)) throw new ArgumentException("A provider entry type is required.", nameof(typeName));
            Id = id; FilePath = filePath; TypeName = typeName;
            var captured = (images ?? throw new ArgumentNullException(nameof(images))).ToArray();
            if (captured.Any(image => image == null)) throw new ArgumentException("Provider images cannot be null.", nameof(images));
            Images = Array.AsReadOnly(captured);
        }

        public string Id { get; }
        public string FilePath { get; }
        public string TypeName { get; }
        public IReadOnlyList<ExportAssemblyImage> Images { get; }

        public static void ValidateId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Any(c => !(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z')
                && !(c >= '0' && c <= '9') && c != '.' && c != '_' && c != '-'))
                throw new ArgumentException("An export provider ID must contain only letters, digits, '.', '_' or '-'.", nameof(id));
        }
    }

    public sealed class ExportAssemblyImage
    {
        private readonly byte[] _image;
        public ExportAssemblyImage(string filePath, byte[] image)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("An assembly path is required.", nameof(filePath));
            FilePath = filePath;
            _image = (byte[])(image ?? throw new ArgumentNullException(nameof(image))).Clone();
        }
        public string FilePath { get; }
        public byte[] GetImage() => (byte[])_image.Clone();
    }
}
