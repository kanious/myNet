﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace myNet
{
    public struct Const<T>
    {
        public T Value { get; private set; }

        public Const(T value)
            : this()
        {
            this.Value = value;
        }
    }
}
