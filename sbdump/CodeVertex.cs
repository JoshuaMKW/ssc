using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace arookas
{
    class CodeVertex
    {
        public VertexType Type;
        public long BranchTo = -1;
        public string Code;
        public long Addr;

        public CodeVertex(VertexType Typ, long BrTo,string Cde,long Adr)
        {
            Type = Typ;
            BranchTo = BrTo;
            Code = Cde;
            Addr = Adr;
        }
    }
}
