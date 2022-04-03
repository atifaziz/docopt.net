// Licensed under terms of MIT license (see LICENSE-MIT)
// Copyright 2012 Vladimir Keleshev, 2013 Dinh Doan Van Bien, 2021 Atif Aziz

namespace DocoptNet.Internals
{
    using System.Globalization;

    class Argument: LeafPattern
    {
        public Argument(string name) : base(name, ArgValue.None) { }

        public Argument(string name, string value) :
            base(name, value) { }

        public Argument(string name, string[] values) :
            base(name, StringList.BottomTop(values)) { }

        /// <remarks>
        /// This is only used by tests as a convenience. The instantiated
        /// <see cref="ArgValue"/> is a string representation of the integer.
        /// </remarks>

        public Argument(string name, int value) :
            this(name, value.ToString(CultureInfo.InvariantCulture)) { }
    }
}