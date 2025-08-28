using System.Diagnostics;
using System;

namespace Import3D.FBX {
	/** Represents a link between two FBX objects. */
	public unsafe class FBXConnection {
		public FBXConnection(UInt64 insertionOrder, UInt64 src, UInt64 dest, string prop, FBXDocument doc) {
			this.insertionOrder = insertionOrder; this.prop = prop;
			this.src = src; this.dest = dest; this.doc = doc;
			System.Diagnostics.Debug.Assert(doc.Objects().find(src) != doc.Objects().end());
			// dest may be 0 (root node)
			System.Diagnostics.Debug.Assert(!dest || doc.Objects().find(dest) != doc.Objects().end());

		}


		// note: a connection ensures that the source and dest objects exist, but
		// not that they have DOM representations, so the return value of one of
		// these functions can still be null.
		FBXObject SourceObject() {
			FBXLazyObject* lazy = doc.GetObject(src);
			System.Diagnostics.Debug.Assert(lazy);
			if (lazy == null) {
				return null;
			}

			return lazy.Get();

		}
		FBXObject DestinationObject() {
			FBXLazyObject* lazy = doc.GetObject(dest);
			System.Diagnostics.Debug.Assert(lazy);
			if (lazy == null) {
				return null;
			}

			return lazy.Get();

		}

		// these, however, are always guaranteed to be valid
		FBXLazyObject LazySourceObject() {
			FBXLazyObject* lazy = doc.GetObject(src);
			System.Diagnostics.Debug.Assert(lazy);
			return *lazy;

		}
		FBXLazyObject LazyDestinationObject() {
			FBXLazyObject* lazy = doc.GetObject(dest);
			System.Diagnostics.Debug.Assert(lazy);
			return *lazy;

		}

		/** return the name of the property the connection is attached to.
         * this is an empty string for object to object (OO) connections. */
		string PropertyName() {
			return prop;
		}

		UInt64 InsertionOrder() {
			return insertionOrder;
		}

		int CompareTo(FBXConnection c) {
			Debug.Assert(null != c);

			// note: can't subtract because this would overflow UInt64
			if (InsertionOrder() > c.InsertionOrder()) {
				return 1;
			}
			else if (InsertionOrder() < c.InsertionOrder()) {
				return -1;
			}
			return 0;
		}

		bool Compare(FBXConnection c) {
			Debug.Assert(null != c);

			return InsertionOrder() < c.InsertionOrder();
		}


		UInt64 insertionOrder;
		string prop;

		UInt64 src, dest;
		FBXDocument doc;
	}
}
