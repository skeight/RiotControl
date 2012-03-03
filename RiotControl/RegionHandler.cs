﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Npgsql;
using NpgsqlTypes;

using LibOfLegends;

using com.riotgames.platform.statistics;
using com.riotgames.platform.summoner;

namespace RiotControl
{
	class RegionHandler
	{
		EngineRegionProfile RegionProfile;
		NpgsqlConnection Database;
		RPCService RPC;

		public RegionHandler(Configuration configuration, EngineRegionProfile regionProfile, NpgsqlConnection database)
		{
			RegionProfile = regionProfile;
			Database = database;
			if (regionProfile.Logins.Count != 1)
				throw new Exception("Currently the number of accounts per region is limited to one");
			Login login = regionProfile.Logins.First();
			ConnectionProfile connectionData = new ConnectionProfile(configuration.Authentication, regionProfile.Region, configuration.Proxy, login.Username, login.Password);
			RPC = new RPCService(connectionData);
			WriteLine("Connecting to the server");
			RPC.Connect(OnConnect);
		}

		void WriteLine(string input, params object[] arguments)
		{
			Nil.Output.WriteLine(string.Format("{0} [{1}] {2}", Nil.Time.Timestamp(), RegionProfile.Abbreviation, input), arguments);
		}

		void SummonerMessage(string message, Summoner summoner, params object[] arguments)
		{
			WriteLine(string.Format("{0} ({1}): {2}", summoner.Name, summoner.AccountId, message), arguments);
		}

		void OnConnect(bool connected)
		{
			if (connected)
			{
				WriteLine("Successfully connected to the server");
				//OnConnect must return so we can't use the current thread to execute region handler logic.
				//This is a limitation imposed by FluorineFX.
				(new Thread(Run)).Start();
			}
			else
				WriteLine("There was an error connecting to the server");
		}

		void ProcessSummary(string mapEnum, string queueModeEnum, string target, Summoner summoner, List<PlayerStatSummary> summaries, bool forceNullRating = false)
		{
			foreach (var summary in summaries)
			{
				if (summary.playerStatSummaryType != target)
					continue;
				NpgsqlCommand update = new NpgsqlCommand("update summoner_rating set wins = :wins, losses = :losses, leaves = :leaves, current_rating = :current_rating, top_rating = :top_rating where summoner_id = :summoner_id and rating_map = cast(:rating_map as map_type) and queue_mode = cast(:queue_mode as queue_mode_type)", Database);
				if (forceNullRating)
				{
					update.Set("current_rating", NpgsqlDbType.Integer, null);
					update.Set("top_rating", NpgsqlDbType.Integer, null);
				}
				else
				{
					update.Set("current_rating", summary.rating);
					update.Set("top_rating", summary.maxRating);
				}

				update.Set("summoner_id", summoner.Id);
				update.SetEnum("rating_map", mapEnum);
				update.SetEnum("queue_mode", queueModeEnum);

				update.Set("wins", summary.wins);
				update.Set("losses", summary.losses);
				update.Set("leaves", summary.leaves);

				int rowsAffected = update.ExecuteNonQuery();
				if (rowsAffected == 0)
				{
					//We're dealing with a new summoner rating entry, insert it
					NpgsqlCommand insert = new NpgsqlCommand("insert into summoner_rating (summoner_id, rating_map, queue_mode, wins, losses, leaves, current_rating, top_rating) values (:summoner_id, cast(:rating_map as map_type), cast(:queue_mode as queue_mode_type), :wins, :losses, :leaves, :current_rating, :top_rating)", Database);
					insert.Parameters.AddRange(update.Parameters.ToArray());
					insert.ExecuteNonQuery();
					SummonerMessage(string.Format("New rating for mode {0}", target), summoner);
				}
				else
				{
					//This rating was already in the database and was updated
					SummonerMessage(string.Format("Updated rating for mode {0}", target), summoner);
				}
				break;
			}
		}

		void UpdateSummoner(Summoner summoner, bool isNewSummoner)
		{
			PlayerLifeTimeStats lifeTimeStatistics = RPC.RetrievePlayerStatsByAccountID(summoner.AccountId, "CURRENT");
			if (lifeTimeStatistics == null)
			{
				SummonerMessage("Unable to retrieve lifetime statistics", summoner);
				return;
			}

			AggregatedStats aggregatedStatistics = RPC.GetAggregatedStats(summoner.AccountId, "CLASSIC", "CURRENT");
			if (aggregatedStatistics == null)
			{
				SummonerMessage("Unable to retrieve aggregated statistics", summoner);
				return;
			}

			List<PlayerStatSummary> summaries = lifeTimeStatistics.playerStatSummaries.playerStatSummarySet;

			ProcessSummary("summoners_rift", "normal", "Unranked", summoner, summaries, true);
			ProcessSummary("twisted_treeline", "premade", "RankedPremade3x3", summoner, summaries);
			ProcessSummary("summoners_rift", "solo", "RankedSolo5x5", summoner, summaries);
			ProcessSummary("summoners_rift", "premade", "RankedPremade5x5", summoner, summaries);
			ProcessSummary("dominion", "normal", "OdinUnranked", summoner, summaries);

			List<string> fields = new List<string>()
			{
				"summoner_id",

				"champion_id",

				"wins",
				"losses",

				"kills",
				"deaths",
				"assists",

				"minion_kills",
				
				"gold",
				
				"turrets_destroyed",
				
				"damage_dealt",
				"physical_damage_dealt",
				"magical_damage_dealt",
				
				"damage_taken",
				
				"double_kills",
				"triple_kills",
				"quadra_kills",
				"penta_kills",
				
				"time_spent_dead",
				
				"maximum_kills",
				"maximum_deaths",
			};

			List<ChampionStatistics> statistics = ChampionStatistics.TranslateAggregatedStatistics(aggregatedStatistics);
			foreach (var champion in statistics)
			{
				SQLCommand championUpdate = new SQLCommand("update summoner_ranked_statistics set wins = :wins, losses = :losses, kills = :kills, deaths = :deaths, assists = :assists, minion_kills = :minion_kills, gold = :gold, turrets_destroyed = :turrets_destroyed, damage_dealt = :damage_dealt, physical_damage_dealt = :physical_damage_dealt, magical_damage_dealt = :magical_damage_dealt, damage_taken = :damage_taken, double_kills = :double_kills, triple_kills = :triple_kills, quadra_kills = :quadra_kills, penta_kills = :penta_kills, time_spent_dead = :time_spent_dead, maximum_kills = :maximum_kills, maximum_deaths = :maximum_deaths where summoner_id = :summoner_id and champion_id = :champion_id", Database);
				championUpdate.SetFieldNames(fields);

				championUpdate.Set(summoner.Id);

				championUpdate.Set(champion.ChampionId);

				championUpdate.Set(champion.Wins);
				championUpdate.Set(champion.Losses);

				championUpdate.Set(champion.Kills);
				championUpdate.Set(champion.Deaths);
				championUpdate.Set(champion.Assists);

				championUpdate.Set(champion.MinionKills);

				championUpdate.Set(champion.Gold);

				championUpdate.Set(champion.TurretsDestroyed);

				championUpdate.Set(champion.DamageDealt);
				championUpdate.Set(champion.PhysicalDamageDealt);
				championUpdate.Set(champion.MagicalDamageDealt);

				championUpdate.Set(champion.DamageTaken);

				championUpdate.Set(champion.DoubleKills);
				championUpdate.Set(champion.TripleKills);
				championUpdate.Set(champion.QuadraKills);
				championUpdate.Set(champion.PentaKills);

				championUpdate.Set(champion.TimeSpentDead);

				championUpdate.Set(champion.MaximumKills);
				championUpdate.Set(champion.MaximumDeaths);

				int rowsAffected = championUpdate.Execute();

				if (rowsAffected == 0)
				{
					//The champion entry didn't exist yet so we must create a new entry first
					string queryFields = GetGroupString(fields);
					string queryValues = GetPlaceholderString(fields);
					SQLCommand championInsert = new SQLCommand(string.Format("insert into summoner_ranked_statistics ({0}) values ({1})", queryFields, queryValues), Database);
					championInsert.CopyParameters(championUpdate);
					championInsert.Execute();
				}
			}

			if (!isNewSummoner)
			{
				//This means that the main summoner entry must be updated
				NpgsqlCommand timeUpdate = new NpgsqlCommand(string.Format("update summoner set time_updated = {0} where id = :id", CurrentTimestamp()), Database);
				timeUpdate.Set("id", summoner.Id);
				timeUpdate.ExecuteNonQuery();
			}
		}

		string GetGroupString(List<string> fields)
		{
			return String.Join(", ", fields);
		}

		string GetPlaceholderString(List<string> fields)
		{
			var mapped = from x in fields
						 select ":" + x;
			return GetGroupString(mapped.ToList());
		}

		string Zulufy(string input)
		{
			return string.Format("{0} at time zone 'UTC'", input);
		}

		string CurrentTimestamp()
		{
			return Zulufy("current_timestamp");
		}

		void UpdateSummonerByName(string summonerName)
		{
			NpgsqlCommand nameLookup = new NpgsqlCommand("select id, account_id from summoner where region = cast(:region as region_type) and summoner_name = :name", Database);
			nameLookup.SetEnum("region", RegionProfile.RegionEnum);
			nameLookup.Set("name", NpgsqlDbType.Text, summonerName);
			NpgsqlDataReader reader = nameLookup.ExecuteReader();
			if (reader.Read())
			{
				//The summoner already exists in the database
				int id = (int)reader[0];
				int accountId = (int)reader[1];
				UpdateSummoner(new Summoner(summonerName, id, accountId), false);
			}
			else
			{
				//We are dealing with a new summoner
				PublicSummoner summoner = RPC.GetSummonerByName(summonerName);
				if (summoner == null)
				{
					WriteLine("No such summoner: {0}", summonerName);
					return;
				}
				List<string> coreFields = new List<string>()
				{
					"account_id",
					"summoner_id",
					"summoner_name",
					"internal_name",
					"summoner_level",
					"profile_icon",
				};

				List<string> extendedFields = new List<string>()
				{
					"time_created",
					"time_updated",
				};

				var field = coreFields.GetEnumerator();

				string fieldsString = string.Format("region, {0}", GetGroupString(coreFields.Concat(extendedFields).ToList()));
				string placeholderString = GetPlaceholderString(coreFields);
				string valuesString = string.Format("cast(:region as region_type), {0}, {1}, {1}", placeholderString, CurrentTimestamp());
				string query = string.Format("insert into summoner ({0}) values ({1})", fieldsString, valuesString);

				NpgsqlCommand newSummoner = new NpgsqlCommand(query, Database);

				newSummoner.SetEnum("region", RegionProfile.RegionEnum);
				newSummoner.Set(ref field, summoner.acctId);
				newSummoner.Set(ref field, summoner.summonerId);
				newSummoner.Set(ref field, summonerName);
				newSummoner.Set(ref field, summoner.internalName);
				newSummoner.Set(ref field, summoner.summonerLevel);
				newSummoner.Set(ref field, summoner.profileIconId);

				newSummoner.ExecuteNonQuery();

				int id = GetInsertId("summoner");
				UpdateSummoner(new Summoner(summonerName, id, summoner.acctId), true);
			}
			reader.Close();
		}

		int GetInsertId(string tableName)
		{
			NpgsqlCommand currentValue = new NpgsqlCommand(string.Format("select currval('{0}_id_seq')", tableName), Database);
			object result = currentValue.ExecuteScalar();
			long id = (long)result;
			return (int)id;
		}

		void Run()
		{
			NpgsqlCommand getJob = new NpgsqlCommand("select id, summoner_name from lookup_job where region = cast(:region as region_type) order by priority desc, time_added limit 1", Database);
			getJob.SetEnum("region", RegionProfile.RegionEnum);

			NpgsqlCommand deleteJob = new NpgsqlCommand("delete from lookup_job where id = :id", Database);
			deleteJob.Add("id", NpgsqlDbType.Integer);

			while (true)
			{
				NpgsqlTransaction lookupTransaction = Database.BeginTransaction();
				NpgsqlDataReader reader = getJob.ExecuteReader();
				bool success = reader.Read();

				int id;
				String summonerName = null;

				if (success)
				{
					id = (int)reader[0];
					summonerName = (string)reader[1];

					//Delete entry
					//deleteCommand.Parameters[0].Value = id;
					//deleteCommand.ExecuteNonQuery();
				}
				lookupTransaction.Commit();

				if (success)
					UpdateSummonerByName(summonerName);
				else
				{
					WriteLine("No jobs available");
					//Should wait for an event here really
					break;
				}

				reader.Close();
			}
		}
	}
}