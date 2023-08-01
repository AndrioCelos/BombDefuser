﻿using System.Reflection;

namespace BombDefuserScripts;
public abstract class ModuleScript {
	private static readonly Dictionary<Type, (Type scriptType, string topic)> Scripts = new(from t in typeof(ModuleScript).Assembly.GetTypes() where t.BaseType is Type t2 && t2.IsGenericType && t2.GetGenericTypeDefinition() == typeof(ModuleScript<>)
																							select new KeyValuePair<Type, (Type, string)>(t.BaseType!.GenericTypeArguments[0], (t, t.GetCustomAttribute<AimlInterfaceAttribute>()?.Topic ?? throw new InvalidOperationException($"Missing topic on {t.Name}"))));
	private static readonly MethodInfo CreateMethodBase = typeof(ModuleScript).GetMethod(nameof(CreateInternal), BindingFlags.NonPublic| BindingFlags.Static)!;
	private static readonly Dictionary<Type, MethodInfo> CreateMethodCache = new();

	private protected string? topic;
	public string Topic => topic ?? throw new InvalidOperationException("Script not yet initialised");

	public abstract string IndefiniteDescription { get; }

	public static ModuleScript Create(ComponentProcessor processor) {
		var processorType = processor.GetType();
		var (scriptType, topic) = Scripts[processorType];
		if (!CreateMethodCache.TryGetValue(scriptType, out var method)) {
			method = CreateMethodBase.MakeGenericMethod(scriptType, processorType);
			CreateMethodCache[scriptType] = method;
		}
		return (ModuleScript) method.Invoke(null, new object[] { processor, topic })!;
	}

	private static TScript CreateInternal<TScript, TProcessor>(TProcessor processor, string topic) where TScript : ModuleScript<TProcessor>, new() where TProcessor : ComponentProcessor
		=> new() { processor = processor, topic = topic };

	public virtual void Enter(AimlAsyncContext context) { }

	protected static async Task<T> ReadCurrentAsync<T>(ComponentProcessor<T> processor) where T : notnull {
		var ss = await AimlTasks.TakeScreenshotAsync();
		return BombDefuserAimlService.Instance.ReadComponent(ss, processor, Utils.CurrentModulePoints);
	}
}

public abstract class ModuleScript<TProcessor> : ModuleScript where TProcessor : ComponentProcessor {
	internal TProcessor? processor;
	protected TProcessor Processor => processor ?? throw new InvalidOperationException("Script not yet initialised");

	protected static TProcessor GetProcessor() => BombDefuserAimlService.GetComponentProcessor<TProcessor>();
}