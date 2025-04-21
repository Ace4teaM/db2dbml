using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace db2dbml
{
    internal enum GenericDataType
    {
        unknown,
        integer,
        date,
        varchar,
        time,
        timestamp,
        autoincrement,
        real,
        chars,
        bytes,
        bits,
        boolean
    }
}
