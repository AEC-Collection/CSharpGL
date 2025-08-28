using System;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace Import3D.FBX {
	/** DOM root for a FBX file */
	public unsafe class FBXDocument {
		static uint LowerSupportedVersion = 7100;
		static uint UpperSupportedVersion = 7400;
		int MAX_CLASSNAMES = 6;

		public FBXDocument(FBXParser parser, FBXImportSettings settings) {
			this.parser = parser; this.settings = settings;
			Log.WriteLine("Creating FBX Document");

			// Cannot use array default initialization syntax because vc8 fails on it
			for (auto & timeStamp : creationTimeStamp) {
				timeStamp = 0;
			}

			ReadHeader();
			ReadPropertyTemplates();

			ReadGlobalSettings();

			// This order is important, connections need parsed objects to check
			// whether connections are ok or not. Objects may not be evaluated yet,
			// though, since this may require valid connections.
			ReadObjects();
			ReadConnections();

		}


		FBXLazyObject GetObject(UInt64 id);

		bool IsBinary() {
			return parser.IsBinary();
		}

		uint FBXVersion() {
			return fbxVersion;
		}

		string Creator() {
			return creator;
		}

		// elements (in this order): Year, Month, Day, Hour, Second, Millisecond
		uint* CreationTimeStamp() {
			return creationTimeStamp;
		}

		FBXFileGlobalSettings GlobalSettings() {
			Debug.Assert(globals.get());
			return *globals;
		}

		Dictionary<string, FBXPropertyTable> Templates() {
			return templates;
		}

		Dictionary<UInt64, FBXLazyObject> Objects() {
			ObjectMap::const_iterator it = objects.find(id);
			return it == objects.end() ? null : (*it).second;

			return objects;
		}

		FBXImportSettings Settings() {
			return settings;
		}

		Dictionary<UInt64, List<FBXConnection>> ConnectionsBySource() {
			return src_connections;
		}

		Dictionary<UInt64, List<FBXConnection>> ConnectionsByDestination() {
			return dest_connections;
		}

		// note: the implicit rule in all DOM classes is to always resolve
		// from destination to source (since the FBX object hierarchy is,
		// with very few exceptions, a DAG, this avoids cycles). In all
		// cases that may involve back-facing edges in the object graph,
		// use LazyObject::IsBeingConstructed() to check.

		List<FBXConnection> GetConnectionsBySourceSequenced(UInt64 source) {
			return GetConnectionsSequenced(source, ConnectionsBySource());

		}
		List<FBXConnection> GetConnectionsByDestinationSequenced(UInt64 dest);

		List<FBXConnection> GetConnectionsBySourceSequenced(UInt64 source, char* classname) {
			char* arr[] = { classname };
			return GetConnectionsBySourceSequenced(src, arr, 1);

		}
		List<FBXConnection> GetConnectionsByDestinationSequenced(UInt64 dest, char* classname);

		List<FBXConnection> GetConnectionsBySourceSequenced(UInt64 source,
				 char** classnames, int count) {
			return GetConnectionsSequenced(source, true, ConnectionsBySource(), classnames, count);

		}
		List<FBXConnection> GetConnectionsByDestinationSequenced(UInt64 dest,
				 char** classnames,
				int count) {
			return GetConnectionsSequenced(dest, false, ConnectionsByDestination(), classnames, count);

		}

		List<FBXAnimationStack> AnimationStacks() {
			if (!animationStacksResolved.empty() || animationStacks.empty()) {
				return animationStacksResolved;
			}

			animationStacksResolved.reserve(animationStacks.Count);
			for (UInt64 id : animationStacks) {
				FBXLazyObject* lazy = GetObject(id);
				FBXAnimationStack* stack = lazy.Get<FBXAnimationStack>();
				if (!lazy || null == stack) {
					Utility.DOMWarning("failed to read AnimationStack object");
					continue;
				}
				animationStacksResolved.push_back(stack);
			}

			return animationStacksResolved;

		}


		List<FBXConnection> GetConnectionsSequenced(UInt64 id, Dictionary<UInt64, List<FBXConnection>> a) {
			List<FBXConnection*> temp;

			std::pair<ConnectionMap::const_iterator, ConnectionMap::const_iterator> range =
					conns.equal_range(id);

			temp.reserve(std::distance(range.first, range.second));
			for (ConnectionMap::const_iterator it = range.first; it != range.second; ++it) {
				temp.push_back((*it).second);
			}

			std::sort(temp.begin(), temp.end(), std::mem_fn(&FBXConnection::Compare));

			return temp; // NRVO should handle this

		}
		List<FBXConnection> GetConnectionsSequenced(UInt64 id, bool is_src,
				 Dictionary<UInt64, List<FBXConnection>> a,
				 byte* classnames,
				int count) {
			System.Diagnostics.Debug.Assert(classnames);
			System.Diagnostics.Debug.Assert(count != 0);
			System.Diagnostics.Debug.Assert(count <= MAX_CLASSNAMES);

			int lengths[MAX_CLASSNAMES] = { };

			int c = count;
			for (int i = 0; i < c; ++i) {
				lengths[i] = strlen(classnames[i]);
			}

			List<FBXConnection*> temp;
			std::pair<ConnectionMap::const_iterator, ConnectionMap::const_iterator> range =
					conns.equal_range(id);

			temp.reserve(std::distance(range.first, range.second));
			for (ConnectionMap::const_iterator it = range.first; it != range.second; ++it) {
				FBXToken & key = (is_src ? (*it).second.LazyDestinationObject() : (*it).second.LazySourceObject()).GetElement().KeyToken();

				char* obtype = key.begin();

				for (int i = 0; i < c; ++i) {
					System.Diagnostics.Debug.Assert(classnames[i]);
					if (static_cast<int>(std::distance(key.begin(), key.end())) == lengths[i] && !strncmp(classnames[i], obtype, lengths[i])) {
						obtype = null;
						break;
					}
				}

				if (obtype) {
					continue;
				}

				temp.push_back((*it).second);
			}

			std::sort(temp.begin(), temp.end(), std::mem_fn(&FBXConnection::Compare));
			return temp; // NRVO should handle this

		}
		void ReadHeader() {
			// Read ID objects from "Objects" section
			FBXScope sc = parser.GetRootScope();
			FBXElement ehead = sc["FBXHeaderExtension"];
			if (!ehead || !ehead.Compound()) {
				DOMError("no FBXHeaderExtension dictionary found");
			}

			FBXScope shead = *ehead.Compound();
			fbxVersion = ParseTokenAsInt(GetRequiredToken(GetRequiredElement(shead, "FBXVersion", ehead), 0));
			Log.WriteLine("FBX Version: ", fbxVersion);

			// While we may have some success with newer files, we don't support
			// the older 6.n fbx format
			if (fbxVersion < LowerSupportedVersion) {
				DOMError("unsupported, old format version, supported are only FBX 2011, FBX 2012 and FBX 2013");
			}
			if (fbxVersion > UpperSupportedVersion) {
				if (Settings().strictMode) {
					DOMError("unsupported, newer format version, supported are only FBX 2011, FBX 2012 and FBX 2013"



							 " (turn off strict mode to try anyhow) ");
				}
				else {
					Utility.DOMWarning("unsupported, newer format version, supported are only FBX 2011, FBX 2012 and FBX 2013,"



							   " trying to read it nevertheless");
				}
			}

			FBXElement* ecreator = shead["Creator"];
			if (ecreator) {
				creator = ParseTokenAsString(GetRequiredToken(*ecreator, 0));
			}

			FBXElement* etimestamp = shead["CreationTimeStamp"];
			if (etimestamp && etimestamp.Compound()) {
				FBXScope stimestamp = *etimestamp.Compound();
				creationTimeStamp[0] = ParseTokenAsInt(GetRequiredToken(GetRequiredElement(stimestamp, "Year"), 0));
				creationTimeStamp[1] = ParseTokenAsInt(GetRequiredToken(GetRequiredElement(stimestamp, "Month"), 0));
				creationTimeStamp[2] = ParseTokenAsInt(GetRequiredToken(GetRequiredElement(stimestamp, "Day"), 0));
				creationTimeStamp[3] = ParseTokenAsInt(GetRequiredToken(GetRequiredElement(stimestamp, "Hour"), 0));
				creationTimeStamp[4] = ParseTokenAsInt(GetRequiredToken(GetRequiredElement(stimestamp, "Minute"), 0));
				creationTimeStamp[5] = ParseTokenAsInt(GetRequiredToken(GetRequiredElement(stimestamp, "Second"), 0));
				creationTimeStamp[6] = ParseTokenAsInt(GetRequiredToken(GetRequiredElement(stimestamp, "Millisecond"), 0));
			}

		}
		void ReadObjects() {
			// read ID objects from "Objects" section
			FBXScope sc = parser.GetRootScope();
			FBXElement* eobjects = sc["Objects"];
			if (!eobjects || !eobjects.Compound()) {
				DOMError("no Objects dictionary found");
			}

			StackAllocator & allocator = parser.GetAllocator();

			// add a dummy entry to represent the Model::RootNode object (id 0),
			// which is only indirectly defined in the input file
			objects[0] = new_LazyObject(0L, *eobjects, *this);

			FBXScope sobjects = *eobjects.Compound();
			for (ElementMap::value_type & el : sobjects.Elements()) {

				// extract ID
				List<FBXToken> tok = el.second.Tokens();

				if (tok.Count == 0) {
					DOMError("expected ID after object key", el.second);
				}

				char* err;
				UInt64 id = ParseTokenAsID(*tok[0], err);
				if (err) {
					DOMError(err, el.second);
				}

				// id=0 is normally implicit
				if (id == 0L) {
					DOMError("encountered object with implicitly defined id 0", el.second);
				}

				auto foundObject = objects.find(id);
				if (foundObject != objects.end()) {
					Utility.DOMWarning("encountered duplicate object id, ignoring first occurrence", el.second);
					delete_LazyObject(foundObject.second);
				}

				objects[id] = new_LazyObject(id, *el.second, *this);

				// grab all animation stacks upfront since there is no listing of them
				if (!strcmp(el.first.c_str(), "AnimationStack")) {
					animationStacks.push_back(id);
				}
			}

		}
		void ReadPropertyTemplates() {
			FBXScope sc = parser.GetRootScope();
			// read property templates from "Definitions" section
			FBXElement* edefs = sc["Definitions"];
			if (!edefs || !edefs.Compound()) {
				Utility.DOMWarning("no Definitions dictionary found");
				return;
			}

			FBXScope sdefs = *edefs.Compound();
			ElementCollection otypes = sdefs.GetCollection("ObjectType");
			for (ElementMap::const_iterator it = otypes.first; it != otypes.second; ++it) {
				FBXElement el = *(*it).second;
				FBXScope curSc = el.Compound();
				if (!curSc) {
					Utility.DOMWarning("expected nested scope in ObjectType, ignoring", el);
					continue;
				}

				List<FBXToken> tok = el.Tokens();
				if (tok.Count == 0) {
					Utility.DOMWarning("expected name for ObjectType element, ignoring", el);
					continue;
				}

				string &oname = ParseTokenAsString(*tok[0]);

				ElementCollection templs = curSc.GetCollection("PropertyTemplate");
				for (ElementMap::const_iterator elemIt = templs.first; elemIt != templs.second; ++elemIt) {
					FBXElement innerEl = *(*elemIt).second;
					FBXScope innerSc = innerEl.Compound();
					if (!innerSc) {
						Utility.DOMWarning("expected nested scope in PropertyTemplate, ignoring", el);
						continue;
					}

					List<FBXToken> curTok = innerEl.Tokens();
					if (curTok.empty()) {
						Utility.DOMWarning("expected name for PropertyTemplate element, ignoring", el);
						continue;
					}

					string &pname = ParseTokenAsString(*curTok[0]);

					FBXElement* Properties70 = (*innerSc)["Properties70"];
					if (Properties70) {
						std::shared_ptr<PropertyTable> props = std::make_shared<PropertyTable>(
								*Properties70, std::shared_ptr<PropertyTable>(static_cast<PropertyTable*>(null)));

						templates[oname + "." + pname] = props;
					}
				}
			}

		}
		void ReadConnections() {
			StackAllocator & allocator = parser.GetAllocator();
			FBXScope sc = parser.GetRootScope();
			// read property templates from "Definitions" section
			FBXElement* econns = sc["Connections"];
			if (!econns || !econns.Compound()) {
				DOMError("no Connections dictionary found");
			}

			UInt64 insertionOrder = 0l;
			FBXScope sconns = *econns.Compound();
			ElementCollection conns = sconns.GetCollection("C");
			for (ElementMap::const_iterator it = conns.first; it != conns.second; ++it) {
				FBXElement el = *(*it).second;
				string &type = ParseTokenAsString(GetRequiredToken(el, 0));

				// PP = property-property connection, ignored for now
				// (tokens: "PP", ID1, "Property1", ID2, "Property2")
				if (type == "PP") {
					continue;
				}

				UInt64 src = ParseTokenAsID(GetRequiredToken(el, 1));
				UInt64 dest = ParseTokenAsID(GetRequiredToken(el, 2));

				// OO = object-object connection
				// OP = object-property connection, in which case the destination property follows the object ID
				string &prop = (type == "OP" ? ParseTokenAsString(GetRequiredToken(el, 3)) : "");

				if (objects.find(src) == objects.end()) {
					Utility.DOMWarning("source object for connection does not exist", el);
					continue;
				}

				// dest may be 0 (root node) but we added a dummy object before
				if (objects.find(dest) == objects.end()) {
					Utility.DOMWarning("destination object for connection does not exist", el);
					continue;
				}

				// add new connection
				FBXConnection* c = new_Connection(insertionOrder++, src, dest, prop, *this);
				src_connections.insert(ConnectionMap::value_type(src, c));
				dest_connections.insert(ConnectionMap::value_type(dest, c));
			}

		}
		void ReadGlobalSettings() {
			FBXScope sc = parser.GetRootScope();
			FBXElement* ehead = sc["GlobalSettings"];
			if (null == ehead || !ehead.Compound()) {
				Utility.DOMWarning("no GlobalSettings dictionary found");
				globals.reset(new FBXFileGlobalSettings(*this, std::make_shared<PropertyTable>()));
				return;
			}

			std::shared_ptr<PropertyTable> props = GetPropertyTable(*this, "", *ehead, *ehead.Compound(), true);

			// double v = PropertyGet<float>( *props.get(), string("UnitScaleFactor"), 1.0 );

			if (!props) {
				DOMError("GlobalSettings dictionary contains no property table");
			}

			globals.reset(new FBXFileGlobalSettings(*this, std::move(props)));

		}


		FBXImportSettings settings;

		Dictionary<UInt64, FBXLazyObject> objects;
		FBXParser parser;

		Dictionary<string, FBXPropertyTable> templates;
		Dictionary<UInt64, List<FBXConnection>> src_connections;
		Dictionary<UInt64, List<FBXConnection>> dest_connections;

		uint fbxVersion;
		string creator;
		uint[] creationTimeStamp = new uint[7];

		List<UInt64> animationStacks;
		List<FBXAnimationStack> animationStacksResolved;

		FBXFileGlobalSettings globals;
	}
}