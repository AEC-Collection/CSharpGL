using System.Xml.Linq;

namespace Import3D.FBX {
	public unsafe class FBXPropertyTable {
		Dictionary<string, FBXElement> lazyProps;
		Dictionary<string, FBXProperty> props;
		FBXPropertyTable templateProps;
		FBXElement element;

		// ------------------------------------------------------------------------------------------------
		public FBXPropertyTable(FBXElement element, PropertyTable templateProps) {
			this.templateProps = templateProps; this.element = element;

			FBXScope scope = GetRequiredScope(element);
			foreach (var v in scope.Elements()) {
				if (v.first != "P") {
					DOMWarning("expected only P elements in property table", v.second);
					continue;
				}

				string name = PeekPropertyName(v.Value);//*v.second);
				if (!name.length()) {
					DOMWarning("could not read property name", v.second);
					continue;
				}

				LazyPropertyMap::const_iterator it = lazyProps.find(name);
				if (it != lazyProps.end()) {
					DOMWarning("duplicate property name, will hide previous value: " + name, v.second);
					continue;
				}

				lazyProps[name] = v.second;
			}
		}
		// ------------------------------------------------------------------------------------------------
		FBXProperty Get(string name) {
			PropertyMap::const_iterator it = props.find(name);
			if (it == props.end()) {
				// hasn't been parsed yet?
				LazyPropertyMap::const_iterator lit = lazyProps.find(name);
				if (lit != lazyProps.end()) {
					props[name] = ReadTypedProperty(*(*lit).second);
					it = props.find(name);

					System.Diagnostics.Debug.Assert(it != props.end());
				}

				if (it == props.end()) {
					// check property template
					if (templateProps) {
						return templateProps.Get(name);
					}

					return null;
				}
			}

			return (*it).second;
		}

		// PropertyTable's need not be coupled with FBX elements so this can be null
		FBXElement GetElement() {
			return element;
		}
		FBXPropertyTable* TemplateProps() {
			return templateProps.get();
		}

		DirectPropertyMap GetUnparsedProperties() {
			DirectPropertyMap result;

			// Loop through all the lazy properties (which is all the properties)
			for (LazyPropertyMap::value_type & currentElement : lazyProps) {

				// Skip parsed properties
				if (props.end() != props.find(currentElement.first)) {
					continue;
				}

				// Read the element's value.
				// Wrap the naked pointer (since the call site is required to acquire ownership)
				std::shared_ptr<FBXProperty> prop = std::shared_ptr<FBXProperty>(ReadTypedProperty(*currentElement.second));

				// FBXElement could not be read. Skip it.
				if (!prop) {
					continue;
				}

				// Add to result
				result[currentElement.first] = prop;
			}

			return result;
		}
		T PropertyGet<T>(FBXPropertyTable &in, string name, T &defaultValue) {
			FBXProperty prop = in.Get(name);
			if (null == prop) {
				return defaultValue;
			}

			// strong typing, no need to be lenient
			TypedProperty<T>* tprop = prop.As<TypedProperty<T>>();
			if (null == tprop) {
				return defaultValue;
			}

			return tprop.Value();
		}
		T PropertyGet<T>(FBXPropertyTable &in, string name, bool &result, bool useTemplate = false) {
			FBXProperty prop = in.Get(name);
			if (null == prop) {
				if (!useTemplate) {
					result = false;
					return T();
				}
				FBXPropertyTable* templ = in.TemplateProps();
				if (null == templ) {
					result = false;
					return T();
				}
				prop = templ.Get(name);
				if (null == prop) {
					result = false;
					return T();
				}
			}

			// strong typing, no need to be lenient
			TypedProperty<T>* tprop = prop.As<TypedProperty<T>>();
			if (null == tprop) {
				result = false;
				return T();
			}

			result = true;
			return tprop.Value();
		}

	}
}