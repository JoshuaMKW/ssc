﻿using arookas.IO.Binary;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace arookas
{
	class sunContext : aDisposable
	{
		aBinaryWriter writer;
		uint textOffset, dataOffset, symbolOffset;

		public sunWriter Text { get; private set; }
		public sunDataTable DataTable { get; private set; }
		public sunSymbolTable SymbolTable { get; private set; }
		public sunScopeStack Scopes { get; private set; }
		public sunLoopStack Loops { get; private set; }
		public sunImportResolver ImportResolver { get; private set; }

		// open/close
		public sunContext(Stream output)
			: this(output, sunImportResolver.Default)
		{

		}
		public sunContext(Stream output, sunImportResolver importResolver)
		{
			if (output == null)
			{
				throw new ArgumentNullException("output");
			}
			if (importResolver == null)
			{
				throw new ArgumentNullException("importResolver");
			}
			DataTable = new sunDataTable();
			SymbolTable = new sunSymbolTable();
			Scopes = new sunScopeStack();
			Loops = new sunLoopStack();
			ImportResolver = importResolver;

			writer = new aBinaryWriter(output, Endianness.Big, Encoding.GetEncoding(932));
			Text = new sunWriter(writer);
			writer.PushAnchor();

			WriteHeader(); // dummy header

			// begin text block
			textOffset = (uint)writer.Position;
			writer.PushAnchor(); // match code offsets and writer offsets

			// add system builtins
			DeclareSystemBuiltin("yield", false);
			DeclareSystemBuiltin("exit", false);
			DeclareSystemBuiltin("dump", false);
			DeclareSystemBuiltin("lock", false);
			DeclareSystemBuiltin("unlock", false);
			DeclareSystemBuiltin("int", false, "x");
			DeclareSystemBuiltin("float", false, "x");
			DeclareSystemBuiltin("typeof", false, "x");
			DeclareSystemBuiltin("print", true);
		}
		protected override bool Dispose(bool destructor)
		{
			if (!destructor)
			{
				writer.PopAnchor();
				dataOffset = (uint)writer.Position;
				DataTable.Write(writer);
				symbolOffset = (uint)writer.Position;
				SymbolTable.Write(writer);
				writer.Goto(0);
				WriteHeader();
				return true; // don't dispose the writer so the stream doesn't get disposed
			}
			return false;
		}

		// imports/compilation
		public sunImportResult Import(string name)
		{
			if (name == null)
			{
				throw new ArgumentNullException("name");
			}
			sunScriptFile file;
			var result = ImportResolver.ResolveImport(name, out file);
			if (result == sunImportResult.Loaded)
			{
				try
				{
					ImportResolver.EnterFile(file);
					var parser = new sunParser();
					var tree = parser.Parse(file);
					tree.Compile(this);
					ImportResolver.ExitFile(file);
				}
				finally
				{
					file.Dispose();
				}
			}
			return result;
		}

		// builtins
		public sunBuiltinInfo DeclareBuiltin(sunBuiltinDeclaration node)
		{
			var symbolInfo = SymbolTable.Callables.FirstOrDefault(f => f.Name == node.Builtin.Value);
			if (symbolInfo != null)
			{
				throw new sunRedeclaredBuiltinException(node);
			}
			var builtinInfo = new sunBuiltinInfo(node.Builtin.Value, node.Parameters.ParameterInfo, SymbolTable.Count);
			SymbolTable.Add(builtinInfo);
			return builtinInfo;
		}
		public sunBuiltinInfo DeclareSystemBuiltin(string name, bool variadic, params string[] parameters)
		{
			var builtinInfo = SymbolTable.Builtins.FirstOrDefault(f => f.Name == name);
			if (builtinInfo == null)
			{
				builtinInfo = new sunBuiltinInfo(name, new sunParameterInfo(parameters, variadic), SymbolTable.Count);
				SymbolTable.Add(builtinInfo);
			}
			return builtinInfo;
		}
		public sunBuiltinInfo ResolveSystemBuiltin(string name)
		{
			return SymbolTable.Builtins.FirstOrDefault(f => f.Name == name);
		}

		// functions
		public sunFunctionInfo DefineFunction(sunFunctionDefinition node)
		{
			if (node.Parameters.IsVariadic)
			{
				throw new sunVariadicFunctionException(node);
			}
			var symbolInfo = SymbolTable.Callables.FirstOrDefault(f => f.Name == node.Function.Value);
			if (symbolInfo != null)
			{
				throw new sunRedefinedFunctionException(node);
			}
			var functionInfo = new sunFunctionInfo(node.Function.Value, node.Parameters.ParameterInfo, node.Body);
			SymbolTable.Add(functionInfo);
			return functionInfo;
		}
		public sunCallableSymbolInfo ResolveCallable(sunFunctionCall node)
		{
			var symbolInfo = SymbolTable.Callables.FirstOrDefault(f => f.Name == node.Function.Value);
			if (symbolInfo == null)
			{
				throw new sunUndefinedFunctionException(node);
			}
			return symbolInfo;
		}

		// variables
		public sunVariableInfo DeclareVariable(sunIdentifier node)
		{
			// assert variable is not already declared in current scope
			if (Scopes.Top.GetIsVariableDeclared(node.Value))
			{
				throw new sunRedeclaredVariableException(node);
			}
			var variableInfo = Scopes.DeclareVariable(node.Value);
			if (Scopes.Top.Type == sunScopeType.Script)
			{
				// script variables are added to the symbol table
				SymbolTable.Add(variableInfo);
			}
			return variableInfo;
		}
		public sunVariableInfo ResolveVariable(sunIdentifier node)
		{
			// walk the stack backwards to resolve to the variable's latest declaration
			for (int i = Scopes.Count - 1; i >= 0; --i)
			{
				var variableInfo = Scopes[i].ResolveVariable(node.Value);
				if (variableInfo != null)
				{
					return variableInfo;
				}
			}
			throw new sunUndeclaredVariableException(node);
		}

		public sunVariableInfo DeclareParameter(string name) { return Scopes.DeclareVariable(name); }

		// constants
		public sunConstInfo DeclareConstant(sunIdentifier node, sunExpression expression)
		{
			if (Scopes.Top.GetIsConstantDeclared(node.Value))
			{
				throw new sunRedeclaredVariableException(node);
			}
			var constInfo = Scopes.Top.DeclareConstant(node.Value, expression);
			return constInfo;
		}
		public sunConstInfo ResolveConstant(sunIdentifier node)
		{
			// walk the stack backwards to resolve to the variable's latest declaration
			for (int i = Scopes.Count - 1; i >= 0; --i)
			{
				var constInfo = Scopes[i].ResolveConstant(node.Value);
				if (constInfo != null)
				{
					return constInfo;
				}
			}
			throw new sunUndeclaredVariableException(node);
		}

		public void ResolveVariableOrConstant(sunIdentifier node, out sunVariableInfo variableInfo, out sunConstInfo constInfo)
		{
			try
			{
				variableInfo = ResolveVariable(node);
			}
			catch
			{
				variableInfo = null;
			}
			try
			{
				constInfo = ResolveConstant(node);
			}
			catch
			{
				constInfo = null;
			}
		}

		void WriteHeader()
		{
			writer.WriteString("SPCB");
			writer.Write32(textOffset);
			writer.Write32(dataOffset);
			writer.WriteS32(DataTable.Count);
			writer.Write32(symbolOffset);
			writer.WriteS32(SymbolTable.Count);
			writer.WriteS32(Scopes.Root.VariableCount);
		}
	}

	public class sunFileArgs : EventArgs
	{
		public sunScriptFile File { get; private set; }

		public sunFileArgs(sunScriptFile file)
		{
			if (file == null)
			{
				throw new ArgumentNullException("file");
			}
			File = file;
		}
	}
}
