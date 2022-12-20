using AddressablesTools.JSON;
using System;
using System.Collections.Generic;
using System.Text;

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

        internal void Write(SerializedTypeJson type)
        {
            type.m_AssemblyName = AssemblyName;
            type.m_ClassName = ClassName;
        }
    }
}
