﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Octokit;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Markdown;

internal class Helper
{
	public static string StripMarkdown(string markdownText) // generated by ChatGPT
	{
		// Remove fenced code blocks (``` ... ```)
		markdownText = Regex.Replace(markdownText, @"```[\s\S]*?```", "", RegexOptions.Multiline);

		// Remove headers (lines starting with one or more #)
		markdownText = Regex.Replace(markdownText, @"^\s*#+.*$", "", RegexOptions.Multiline);

		// Remove blockquotes (lines starting with >)
		markdownText = Regex.Replace(markdownText, @"^\s*>.*$", "", RegexOptions.Multiline);

		// Remove all newlines (both \r and \n)
		markdownText = Regex.Replace(markdownText, @"\r?\n", " ");

		return markdownText;
	}

	public static List<Issue> FetchIssues(GitHubClient client, string repositoryOwner, string repositoryName, string milestoneName = null)
	{
		// Issue request settings
		RepositoryIssueRequest issueRequest = new()
		{
			State = ItemStateFilter.Open,
			Milestone = milestoneName
		};

		// Fetch repository issues
		Console.WriteLine("Fetching repository issues:");
		IReadOnlyList<Issue> issues = client.Issue.GetAllForRepository(repositoryOwner, repositoryName).Result;
		List<Issue> sanitizedIssues = new();
		foreach (Issue issue in issues)
		{
			if (issue.PullRequest != null) continue;
			Console.WriteLine($"- {issue.Title}");
			sanitizedIssues.Add(issue);
		}
		return sanitizedIssues;
	}

	public static List<Milestone> FetchMilestones(GitHubClient client, string repositoryOwner, string repositoryName)
	{
		// Milestone request settings
		MilestoneRequest milestoneRequest = new()
		{
			State = ItemStateFilter.Open,
		};

		Console.WriteLine("Fetching repository milestones:");
		IReadOnlyList<Milestone> milestones = client.Issue.Milestone.GetAllForRepository(repositoryOwner, repositoryName, milestoneRequest).Result;
		foreach (Milestone milestone in milestones)
		{
			Console.WriteLine($"- {milestone.Title}");
		}
		return milestones.ToList();
	}

	public static string IssuesToMarkdown(GitHubClient client, string repositoryOwner, string repositoryName, IEnumerable<Issue> issues)
	{
		StringWriter markdown = new();
		markdown.WriteLine($"# {repositoryName} Issues");

		foreach (Issue issue in issues)
		{
			markdown.WriteLine($"### {issue.Title}");

			Milestone milestone = issue.Milestone;
			string milestoneText = milestone == null ? "None" : $"{issue.Milestone.Title} ({milestone.DueOn.Value.Date.ToShortDateString()})";
			markdown.WriteLine($"Milestone: {milestoneText}\n");

			IEnumerable<string> labelStrings = issue.Labels.Select(label => label.Name);
			string labelsText = issue.Labels.Count < 1 ? "None" : string.Join(", ", labelStrings);
			markdown.WriteLine($"Labels: {labelsText}\n");

			IEnumerable<string> assigneeStrings = issue.Assignees.Select(assignee => assignee.Login);
			string assigneeText = issue.Assignees.Count < 1 ? "None" : string.Join(", ", assigneeStrings);
			markdown.WriteLine($"Assignees: {assigneeText}\n");

			IReadOnlyList<IssueComment> comments = client.Issue.Comment.GetAllForIssue(repositoryOwner, repositoryName, issue.Number).Result;
			if (issue.Body != null) markdown.WriteLine($"- {issue.User.Login}: {StripMarkdown(issue.Body)}");
			foreach (IssueComment comment in comments)
			{
				if (comment.Body != null) markdown.WriteLine($"- {comment.User.Login}: {StripMarkdown(comment.Body)}");
			}
		}

		return markdown.ToString();
	}

	public static string MilestonesToMarkdown(GitHubClient client, string repositoryOwner, string repositoryName, IEnumerable<Milestone> milestones)
	{
		StringWriter markdown = new();
		markdown.WriteLine($"# {repositoryName} Milestones");

		foreach (Milestone milestone in milestones)
		{
			markdown.WriteLine($"### {milestone.Title}");

			if (milestone.DueOn is not null) markdown.WriteLine($"{milestone.DueOn.Value.Date.ToShortDateString()}");
			if (milestone.Description.Length > 0) markdown.WriteLine(milestone.Description);

			List<Issue> issues = FetchIssues(client, repositoryOwner, repositoryName, milestone.Title);
			foreach (Issue issue in issues)
			{
				markdown.WriteLine($"- {issue.Title}");

				IEnumerable<string> labelStrings = issue.Labels.Select(label => label.Name);
				string labelsText = issue.Labels.Count < 1 ? "None" : string.Join(", ", labelStrings);
				markdown.WriteLine($"\t- Labels: {labelsText}\n");

				IEnumerable<string> assigneeStrings = issue.Assignees.Select(assignee => assignee.Login);
				string assigneeText = issue.Assignees.Count < 1 ? "None" : string.Join(", ", assigneeStrings);
				markdown.WriteLine($"\t- Assignees: {assigneeText}\n");
			}
		}

		return markdown.ToString();
	}

	public static Document MarkdownToPDF(string markdownText)
	{
		Document document = Document.Create(container =>
		{
			container.Page(page =>
			{
				page.PageColor(Colors.White);
				page.Margin(20);
				page.Content().Markdown(markdownText.ToString());
			});
		});
		return document;
	}
}
