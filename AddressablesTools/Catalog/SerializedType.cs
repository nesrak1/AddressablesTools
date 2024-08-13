using AddressablesTools.JSON;
using AddressablesTools.Reader;
using System;
using System.IO;

namespace AddressablesTools.Catalog
{
    public class SerializedType
    {
        public string AssemblyName { get; set; }
        public string ClassName { get; set; }

        public override bool Equals(object obj)
        {
            return obj is SerializedType type &&
                   AssemblyName == type.AssemblyName &&
                   ClassName == type.ClassName;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(AssemblyName, ClassName);
        }

        internal void Read(SerializedTypeJson type)
        {
            AssemblyName = type.m_AssemblyName;
            ClassName = type.m_ClassName;
        }

        internal void Read(CatalogBinaryReader reader, uint offset)
        {
            reader.BaseStream.Position = offset;

            uint assemblyNameOffset = reader.ReadUInt32();
            uint classNameOffset = reader.ReadUInt32();

            AssemblyName = reader.ReadEncodedString(assemblyNameOffset, '.');
            ClassName = reader.ReadEncodedString(classNameOffset, '.');
        }

        internal void Write(SerializedTypeJson type)
        {
            type.m_AssemblyName = AssemblyName;
            type.m_ClassName = ClassName;
        }

        internal string GetMatchName()
        {
            return GetAssemblyShortName() + "; " + ClassName;
        }

        internal string GetAssemblyShortName()
        {
            if (!AssemblyName.Contains(','))
            {
                throw new InvalidDataException("Assembly name must have commas");
            }

            return AssemblyName.Split(',')[0];
        }
    }
}
