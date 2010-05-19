//
// Search for a user
//
// Author: Miguel de Icaza (miguel@gnome.org)
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.Dialog;
using System.Drawing;
using System.Linq;

namespace TweetStation
{
	// 
	// A view controller that performs a search
	//
	public class SearchViewController : BaseTimelineViewController {
		string search;
		
		public SearchViewController (string search) : base (true)
		{
			this.search = search;
		}

		protected override string TimelineTitle {
			get {
				return search;
			}
		}
		
		protected override void ResetState ()
		{
			Root = Util.MakeProgressRoot (search);
			TriggerRefresh ();
		}
		
		public override void ReloadTimeline ()
		{
			TwitterAccount.CurrentAccount.Download (new Uri ("http://search.twitter.com/search.json?q=" + HttpUtility.UrlEncode (search)), res => {
				if (res == null){
					Root = Util.MakeError ("search");
					return;
				}
				var tweetStream = Tweet.TweetsFromSearchResults (new MemoryStream (res));
				
				Root = new RootElement (search){
					new Section () {
						from tweet in tweetStream select (Element) new TweetElement (tweet)
					}
				};
				ReloadComplete ();
			});
		}
	}
	
	public class SearchElement : RootElement {
		string query;
		
		public SearchElement (string caption, string query) : base (caption)
		{
			this.query = query;
		}

		protected override UIViewController MakeViewController ()
		{
			return new SearchViewController (query) { Account = TwitterAccount.CurrentAccount };
		}
		
	}
	
	public abstract class SearchDialog : DialogViewController {
		protected SearchMirrorElement SearchMirror;
		
		public SearchDialog () : base (null, true)
		{
			EnableSearch = true;
			Style = UITableViewStyle.Plain;
		}
		
		public override void OnSearchTextChanged (string text)
		{
			base.OnSearchTextChanged (text);
			if (SearchMirror != null){
				SearchMirror.Text = text;
				TableView.SetNeedsDisplay ();
			}
		}

		public abstract SearchMirrorElement MakeMirror ();
		public override void ViewWillAppear (bool animated)
		{
			base.ViewWillAppear (animated);
			
			SearchMirror = MakeMirror ();
			Section entries = new Section ();
			if (SearchMirror != null)
				entries.Add (SearchMirror);

			PopulateSearch (entries);
			
			Root = new RootElement (Locale.GetText ("Search")){
				entries,
			};

			StartSearch ();
			PerformFilter ("");
		}
		
		public string GetItemText (NSIndexPath indexPath)
		{
			var element = Root [0][indexPath.Row];
			
			if (element is SearchMirrorElement)
				return ((SearchMirrorElement) element).Text;
			else if (element is StringElement){
				return ((StringElement) element).Caption;
			} else if (element is UserElement) {
				return ((UserElement) element).User.Screenname;
			} else
				throw new Exception ("Unknown item in SearchDialog");
		}
		
		public abstract void PopulateSearch (Section entries);
	}

	public class SearchUser : SearchDialog {
		public SearchUser ()
		{
		}
		
		public override SearchMirrorElement MakeMirror ()
		{
			return new SearchMirrorElement (Locale.GetText ("Go to user `{0}'"));
		}
		
		public override void PopulateSearch (Section entries)
		{
			entries.Add (from x in Database.Main.Query<User> ("SELECT * from User ORDER BY Screenname")
				             select (Element) new UserElement (x));
		}
		
		public override void Selected (NSIndexPath indexPath)
		{
			ActivateController (new FullProfileView (GetItemText (indexPath)));
		}
	}
	
	// 
	// The user selector is just like the user search, but does not activate the
	// nested controller, instead it sets the value and dismisses the controller
	//
	public class UserSelector : SearchUser {
		Action<string> userSelected;
		
		public UserSelector (Action<string> userSelected)
		{
			this.userSelected = userSelected;
		}
		
		public override SearchMirrorElement MakeMirror ()
		{
			 return new SearchMirrorElement (Locale.GetText ("@{0}"));
		}
		
		public override void Selected (NSIndexPath indexPath)
		{
			DismissModalViewControllerAnimated (true);
			userSelected (GetItemText (indexPath));
		}
		
		public override void FinishSearch ()
		{
			base.FinishSearch ();
			DismissModalViewControllerAnimated (true);
		}
	}
	
	public class TwitterTextSearch : SearchDialog {
		public TwitterTextSearch () {}
		List<string> terms = new List<string> ();
		
		public override void PopulateSearch (Section entries)
		{
			int n = Util.Defaults.IntForKey ("searches");
			
			for (int i = 0; i < n; i++){
				var value = Util.Defaults.StringForKey ("u-" + i);
				if (value == null)
					continue;
				terms.Add (value);
				entries.Add (new StringElement (value));
			}
		}
		
		public override SearchMirrorElement MakeMirror ()
		{
			return new SearchMirrorElement (Locale.GetText ("Search `{0}'"));
		}
		
		public override void Selected (NSIndexPath indexPath)
		{
			if (SearchMirror.Text != ""){
				terms.Add (SearchMirror.Text);
				
				Util.Defaults.SetInt (terms.Count, "searches");
				int i = 0;
				foreach (string s in terms)
					Util.Defaults.SetString (s, "u-" + i++);
			}

			ActivateController (new SearchViewController (GetItemText (indexPath)) { Account = TwitterAccount.CurrentAccount });
		}
	}
	
	// 
	// Just a styled string element, but if the search string is not empty
	// the Matches method always returns true
	//
	public class SearchMirrorElement : StyledStringElement {
		string text, format;
		
		public string Text { 
			get { return text; }
			set { text = value; Caption = Locale.Format (format, text); }
		}
		
		public SearchMirrorElement (string format) : base ("")
		{
			this.format = format;
			TextColor = UIColor.FromRGB (0.13f, 0.43f, 0.84f);
			Font = UIFont.BoldSystemFontOfSize (18);
		}
		
		public override bool Matches (string test)
		{
			return !String.IsNullOrEmpty (text);
		}		
	}
}

