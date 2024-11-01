using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System;
using System.Diagnostics;

class InstantDoc : EditorWindow
{
	const string HUB_PATH_KEY = "InstantDoc_HubPath";
	const string DEFAULT_HUB_PATH = "C:/Program Files/Unity Hub/Unity Hub.exe";
	const string IGNORE_SEARCH = "30_search.html";
	const int RESULTS_PER_PAGE = 30;

	int MaxPageIndex => Mathf.FloorToInt((float)searchResults.Count / RESULTS_PER_PAGE);

	int _pageIndex;
	int PageIndex
	{
		get => _pageIndex;
		set
		{
			_pageIndex = value;

			selectedResults = searchResults.GetRange(
				RESULTS_PER_PAGE * _pageIndex,
				_pageIndex == MaxPageIndex ? searchResults.Count % RESULTS_PER_PAGE : RESULTS_PER_PAGE
			);
		}
	}

	GUIStyle boldCenterStyle;
	GUIStyle centerWrapStyle;
	GUIStyle centerGhostStyle;
	GUIStyle centerButtonStyle;
	GUIStyle centerStyle;

	Dictionary<string, string> indexedResults;
	List<string> searchPaths;
	List<string> searchResults;
	List<string> selectedResults;
	InstantDoc window;
	Vector2 scroll;
	string search;
	string lastSearch;
	string docDirPath;
	string hubPath;
	int searchIndex;

	[MenuItem("Tools/InstantDoc")]
	static void ShowWindow()
	{
		InstantDoc instantDoc = GetWindow<InstantDoc>();
		instantDoc.titleContent = new GUIContent("InstantDoc");
		instantDoc.minSize = new Vector2(300, 180);

		instantDoc.CheckDocDir();

		if (string.IsNullOrEmpty(instantDoc.docDirPath))
		{
			instantDoc.hubPath = instantDoc.CheckHubPathValidity(
				EditorPrefs.GetString(
					HUB_PATH_KEY,
					DEFAULT_HUB_PATH
				));

			instantDoc.SearchHubPath(); // needs first execution

			if (string.IsNullOrEmpty(instantDoc.hubPath))
				EditorApplication.update += instantDoc.SearchHubPath;
		}

		instantDoc.window = instantDoc;
		instantDoc.Show();
	}

	void GenerateIfNecessary()
	{
		if (boldCenterStyle == null)
		{
			boldCenterStyle = new GUIStyle(GUI.skin.label)
			{
				alignment = TextAnchor.MiddleCenter,
				fontStyle = FontStyle.Bold
			};
		}

		if (centerWrapStyle == null)
		{
			centerWrapStyle = new GUIStyle(GUI.skin.label)
			{
				alignment = TextAnchor.MiddleCenter,
				wordWrap = true
			};
		}

		if (centerGhostStyle == null)
		{
			centerGhostStyle = new GUIStyle(GUI.skin.label)
			{
				alignment = TextAnchor.MiddleCenter,
				normal = new GUIStyleState() { textColor = Color.grey }
			};
		}

		if (centerButtonStyle == null)
			centerButtonStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleCenter };

		if (centerStyle == null)
			centerStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };

		if (indexedResults == null && !string.IsNullOrEmpty(docDirPath))
			EditorApplication.update += IndexDoc;
	}

	void OnGUI()
	{
		GenerateIfNecessary();

		EditorGUILayout.LabelField("Instant Doc", boldCenterStyle);

		EditorGUILayout.Space();

		if (string.IsNullOrEmpty(docDirPath))
		{
			if (string.IsNullOrEmpty(hubPath))
			{
				EditorGUILayout.LabelField("Searching for Unity Hub install", centerGhostStyle);
				EditorGUILayout.Space();

				Rect rect = EditorGUILayout.BeginVertical();
				{
					EditorGUI.ProgressBar(
						rect,
						(float)searchIndex / searchPaths.Count,
						"Searching for Unity Hub"
					);
				}
				EditorGUILayout.EndVertical();

				EditorGUILayout.Space();
			}

			EditorGUILayout.LabelField(
				"Couldn't find any local Unity documentation for this version of the editor (" +
				Application.unityVersion +
				").\nPlease install documentation module for this version from Unity Hub.",
				centerWrapStyle
			);

			EditorGUILayout.Space();

			EditorGUILayout.BeginHorizontal();
			{
				EditorGUILayout.Space();

				if (GUILayout.Button("Open Unity Hub"))
					Process.Start("\"" + hubPath + "\"");

				EditorGUILayout.Space();
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space();

			EditorGUILayout.BeginHorizontal();
			{
				EditorGUILayout.Space();

				if (GUILayout.Button("Check documentation"))
					CheckDocDir();

				EditorGUILayout.Space();
			}
			EditorGUILayout.EndHorizontal();
		}
		else
		{
			if (searchPaths != null)
			{
				if (searchIndex != searchPaths.Count)
				{
					EditorGUILayout.LabelField("Indexing Unity documentation", centerGhostStyle);
					EditorGUILayout.Space();

					Rect rect = EditorGUILayout.BeginVertical();
					{
						EditorGUI.ProgressBar(
							rect,
							(float)searchIndex / searchPaths.Count,
							"Indexing Unity documentation"
						);
					}
					EditorGUILayout.EndVertical();

					EditorGUILayout.Space();
				}

				EditorGUILayout.BeginHorizontal();
				{
					EditorGUILayout.LabelField("Search : ", GUILayout.Width(50));
					search = EditorGUILayout.TextField(search);
				}
				EditorGUILayout.EndHorizontal();

				if (searchIndex == searchPaths.Count)
				{
					EditorGUILayout.Space();

					EditorGUILayout.BeginHorizontal();
					{
						EditorGUILayout.Space();

						if (!string.IsNullOrWhiteSpace(search) && search.Length > 2 && GUILayout.Button("Search"))
							Search();

						EditorGUILayout.Space();
					}
					EditorGUILayout.EndHorizontal();
				}
			}

			EditorGUILayout.Space();

			EditorGUILayout.LabelField("Search term :");

			EditorGUILayout.BeginHorizontal();
			{
				EditorGUILayout.Space();
				EditorGUILayout.LabelField(lastSearch, boldCenterStyle);
				EditorGUILayout.Space();
			}
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.Space();

			if (indexedResults != null && searchResults != null)
			{
				scroll = EditorGUILayout.BeginScrollView(scroll);
				{
					foreach (string result in selectedResults)
					{
						if (GUILayout.Button(result.Replace('-', '.'), centerButtonStyle))
							Help.BrowseURL(indexedResults[result]);
					}
				}
				EditorGUILayout.EndScrollView();
			}

			if (searchResults != null)
			{
				EditorGUILayout.Space();

				EditorGUILayout.BeginHorizontal();
				{
					EditorGUILayout.Space();

					if (PageIndex > 0 && GUILayout.Button("<"))
						PageIndex--;

					EditorGUILayout.LabelField(
						"page " + (PageIndex + 1) + "/" + (MaxPageIndex + 1),
						centerStyle,
						GUILayout.Width(100)
					);

					if (PageIndex < MaxPageIndex && GUILayout.Button(">"))
						PageIndex++;

					EditorGUILayout.Space();
				}
				EditorGUILayout.EndHorizontal();

				EditorGUILayout.Space();
				EditorGUILayout.Space();
			}
		}
	}

	void CheckDocDir()
	{
		if (!string.IsNullOrEmpty(docDirPath)) return;

		DirectoryInfo editorDir = Directory.GetParent(EditorApplication.applicationPath);
		string docPath = Path.Combine(editorDir.FullName, "Data", "Documentation");

		if (!Directory.Exists(docPath)) return;

		string languageISOCode = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
		List<string> docPaths = new List<string>(Directory.GetDirectories(docPath));

		if (docPaths.Count == 0) return;

		// we found the doc for the current language
		string dirPath = docPaths.Find(item => new DirectoryInfo(item).Name == languageISOCode);

		// find english doc
		if (string.IsNullOrEmpty(dirPath))
			dirPath = docPaths.Find(item => new DirectoryInfo(item).Name == "en");

		// get the first one
		if (string.IsNullOrEmpty(dirPath))
			dirPath = docPaths[0];

		docDirPath = Path.Combine(dirPath, "ScriptReference");

		EditorApplication.update += IndexDoc;
	}

	string CheckHubPathValidity(string path)
	{
		if (!File.Exists(path) || !path.Contains("Unity Hub.exe"))
			return null;

		return path;
	}

	void SearchHubPath()
	{
		// if the window is closed
		if (window == null)
			EditorApplication.update -= SearchHubPath;

		if (searchPaths == null)
		{
			// first iteration
			searchPaths = new List<string>(Directory.GetDirectories(Directory.GetDirectoryRoot(Application.dataPath)));
			searchIndex = 0;

			// remove hidden folders
			searchPaths.RemoveAll(item =>
			{
				foreach (string frag in item.Split("\\"))
				{
					if (frag[0] == '$')
						return true;
				}

				return false;
			});
		}

		string currentPath = searchPaths[searchIndex];

		if (currentPath.Contains("Unity Hub"))
			hubPath = currentPath + "/Unity Hub.exe";
		else
		{
			searchIndex++;
			string[] dirs = null;

			try
			{
				// try for access
				dirs = Directory.GetDirectories(currentPath);
			}
			catch (Exception e) { }

			if (dirs != null)
				searchPaths.AddRange(dirs);
		}

		if (!string.IsNullOrEmpty(hubPath))
		{
			EditorPrefs.SetString(HUB_PATH_KEY, hubPath);
			EditorApplication.update -= SearchHubPath;
			searchPaths = null;
		}
	}

	void IndexDoc()
	{
		// if the window is closed
		if (window == null)
			EditorApplication.update -= IndexDoc;

		if (indexedResults == null)
		{
			indexedResults = new Dictionary<string, string>();
			searchPaths = new List<string>(Directory.GetFiles(docDirPath));
			searchPaths.RemoveAll(item => item.Contains(IGNORE_SEARCH));
			searchIndex = 0;
		}

		if (indexedResults.Count < searchPaths.Count)
		{
			for (int i = 0; i < 100; i++)
			{
				// early finish
				if (indexedResults.Count == searchPaths.Count)
					break;

				indexedResults.Add(
					new FileInfo(searchPaths[searchIndex]).Name.Replace(".html", ""),
					searchPaths[searchIndex]
				);

				searchIndex++;
			}
		}

		if (indexedResults.Count == searchPaths.Count)
			EditorApplication.update -= IndexDoc;
	}

	void Search()
	{
		lastSearch = search;
		List<string>[] results = new List<string>[search.Length - 2];
		string[] partialSearch = new string[search.Length - 2];
		string normSearch = search.ToLower();

		// generate partial search terms (min 3 letters)
		for (int i = normSearch.Length - 1; i >= 2; i--)
			partialSearch[(normSearch.Length - 1) - i] = normSearch.Substring(0, i + 1);

		// check each search term on every keyword
		foreach (string keyword in indexedResults.Keys)
		{
			for (int i = 0; i < partialSearch.Length; i++)
			{
				if (results[i] == null)
					results[i] = new List<string>();

				if (keyword.ToLower().Contains(partialSearch[i]))
				{
					bool isUnique = true;

					for (int j = 0; j < i; j++)
					{
						if (results[j].Contains(keyword))
						{
							isUnique = false;
							break;
						}
					}

					if (isUnique)
						results[i].Add(keyword);
				}
			}
		}

		// sort best results
		foreach (List<string> result in results)
			result.Sort((x, y) => x.Length.CompareTo(y.Length));

		// agregate results
		searchResults = new List<string>();

		for (int i = 0; i < results.Length; i++)
		{
			if (results[i] != null)
				searchResults.AddRange(results[i]);
		}

		PageIndex = 0;
	}
}