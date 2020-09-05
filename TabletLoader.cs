using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using DV.Shops;
using HarmonyLib;
using Object = UnityEngine.Object;
using System.IO;

namespace DvMod.HeadsUpDisplay.Cybex
{
	public class TabletLoader
	{
		private static AssetBundle? assets;

		public static void Init ()
		{
			//assets = AssetBundle.LoadFromFile(Main.mod?.Path + "tabletcomputer");
			assets = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(typeof(TabletLoader).Assembly.Location), "tabletcomputer"));
			Debug.LogWarning($"Tablet Loader Init()");
		}

		public static void CreateShopItems ()
		{
			if (assets == null)
			{
				Debug.LogError("Failed to load tabletcomputer bundle!");
				return;
			}

			Debug.LogWarning($"Tablet Loader: {assets}");

			InventoryItemSpec itemSpec = assets.LoadAsset<GameObject>("TabletComputer").GetComponent<InventoryItemSpec>();

			Debug.LogWarning($"Item spec: {itemSpec}");

			GlobalShopController.Instance.shopItemsData.Add(new ShopItemData()
			{
				item = itemSpec,
				basePrice = 1291,
				amount = 1,
				isGlobal = true
			});

			//GlobalShopController.Instance.initialItemAmounts.Add(1);
			((List<int>)typeof(GlobalShopController).GetField("initialItemAmounts", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(GlobalShopController.Instance)).Add(1);

			Debug.LogWarning("Added global shop data");

			Shop[] shops = Object.FindObjectsOfType<Shop>();
			foreach (Shop shop in shops)
			{
				Debug.LogWarning($"adding to shop {shop}");

				ScanItemResourceModule findMax = shop.scanItemResourceModules.FindMax(r => r.transform.localPosition.x);
				ScanItemResourceModule resource = Object.Instantiate(findMax, findMax.transform.parent);
				resource.gameObject.SetActive(true);
				resource.sellingItemSpec = itemSpec;
				resource.transform.localRotation = findMax.transform.localRotation;
				resource.transform.localPosition = findMax.transform.localPosition + Vector3.right * 1.2f;

				//resource.Start();
				typeof(ScanItemResourceModule).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(resource, null);

				Debug.LogWarning($"new item sign: {resource}");

				var arr = new ScanItemResourceModule[shop.scanItemResourceModules.Length + 1];
				Array.Copy(shop.scanItemResourceModules, 0, arr, 0, shop.scanItemResourceModules.Length);
				arr[arr.Length - 1] = resource;
				shop.cashRegister.resourceMachines = Array.ConvertAll(arr, e => (ResourceModule)e);

				resource.ItemPurchased += GlobalShopController.Instance.AddItemToInstatiationQueue;
			}

			Debug.LogWarning("Tablet Loader: done");
		}

		[HarmonyPatch]
		class Resources_Patch
		{
			static MethodBase TargetMethod()
			{
				return typeof(Resources).GetMethods().Single(m => m.Name == nameof(Resources.Load) && !m.ContainsGenericParameters && m.GetParameters().Length == 1);
			}

			static bool Prefix(string path, ref Object __result)
			{
				const string prefix = "HeadsUpDisplay.";

				Debug.LogWarning($"Resource load: {path}");

				if (path is { } && assets != null && path.StartsWith(prefix))
				{
					__result = assets.LoadAsset(path.Substring(prefix.Length));
					return false;
				}

				return true;
			}
		}
	}

	public static class Helper
	{
		public static T FindMax<T>(this IEnumerable<T> source, Func<T, float> selector)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));
			if (selector == null)
				throw new ArgumentNullException(nameof(selector));
			float f;
			T val;
			using var enumerator = source.GetEnumerator();
			if (!enumerator.MoveNext())
				throw new InvalidOperationException("Sequence contains no elements");
			for (val = enumerator.Current, f = selector(val); float.IsNaN(f); val = enumerator.Current, f = selector(val))
			{
				if (!enumerator.MoveNext())
					return val;
			}
			while (enumerator.MoveNext())
			{
				var num = selector(enumerator.Current);
				if (num > f)
				{
					f = num;
					val = enumerator.Current;
				}
			}

			return val;
		}
	}
}
