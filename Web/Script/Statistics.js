function RankedStatistics(
    championName,

    wins,
    losses,

    kills,
    deaths,
    assists,

    minionKills,

    gold,

    turretsDestroyed,

    damageDealt,
    physicalDamageDealt,
    magicalDamageDealt,

    damageTaken,

    doubleKills,
    tripleKills,
    quadraKills,
    pentaKills,

    timeSpentDead,

    maximumKills,
    maximumDeaths
)
{
    this.championName = championName;

    this.wins = wins;
    this.losses = losses;

    this.kills = kills;
    this.deaths = deaths;
    this.assists = assists;

    this.minionKills = minionKills;

    this.gold = gold;

    this.turretsDestroyed = turretsDestroyed;

    this.damageDealt = damageDealt;
    this.physicalDamageDealt = physicalDamageDealt;
    this.magicalDamageDealt = magicalDamageDealt

    this.damageTaken = damageTaken;

    this.doubleKills = doubleKills;
    this.tripleKills = tripleKills;
    this.quadraKills = quadraKills;
    this.pentaKills = pentaKills;

    this.timeSpentDead = timeSpentDead;

    this.maximumKills = maximumKills;
    this.maximumDeaths = maximumDeaths;

    var gamesPlayed = wins + losses;

    this.gamesPlayed = gamesPlayed;
    this.winLossDifference = wins - losses;

    this.killsPerGame = kills / gamesPlayed;
    this.deathsPerGame = deaths / gamesPlayed;
    this.assistsPerGame = assists / gamesPlayed;

    this.winRatio = wins / gamesPlayed;

    if(deaths > 0)
        this.killsAndAssistsPerDeath = (kills + assists) / deaths;
    else
        this.killsAndAssistsPerDeath = Infinity;

    this.minionKillsPerGame = minionKills / gamesPlayed;
    this.goldPerGame = gold / gamesPlayed;
}

function getContainer()
{
    return document.getElementById('rankedStatistics');
}

function getCell(contents)
{
    return "<td>" + contents + "</td>\n";
}

function getHeadCell(contents)
{
    return "<th>" + contents + "</th>\n";
}

function getSpan(styleClass, content)
{
    return '<span class="' + styleClass + '">' + content + '</span>'
}

function getSignumString(number)
{
    var styleClass;
    if(number > 0)
    {
        styleClass = 'positive';
        number = '+' + number;
    }
    else if(number == 0)
        return '±' + number;
    else
        styleClass = 'negative';
    return getSpan(styleClass, number);
}

function getPercentage(input)
{
    return (input * 100).toFixed(1) + '%';
}

function getPrecisionString(input)
{
    if(input == Infinity)
        return '∞';
    else
        return input.toFixed(1);
}

function getChampionStatisticsRow(statistics)
{
    var fields =
        [
            '<img src="/RiotControl/Static/Image/Champion/Small/' + encodeURI(statistics.championName) + '.png" alt="' + statistics.championName + '">' + statistics.championName + '</a>',
            statistics.gamesPlayed,
            statistics.wins,
            statistics.losses,
            getSignumString(statistics.winLossDifference),
            getPercentage(statistics.wins / statistics.gamesPlayed),
            getPrecisionString(statistics.killsPerGame),
            getPrecisionString(statistics.deathsPerGame),
            getPrecisionString(statistics.assistsPerGame),
            getPrecisionString(statistics.killsAndAssistsPerDeath),
            getPrecisionString(statistics.minionKillsPerGame),
            getPrecisionString(statistics.goldPerGame),
        ]

    var output = "<tr>\n";
    for(var i in fields)
        output += getCell(fields[i]);
    output += "</tr>\n";
    return output;
}

function writeTable(rankedStatistics)
{
    var markup = "<table>\n";
    markup += "<caption>Ranked Statistics</caption>\n";
    var columns =
        [
            'Champion',
            'Games',
            'W',
            'L',
            'W - L',
            'WR',
            'K',
            'D',
            'A',
            '(K + A) / D',
            'MK',
            'Gold',
        ];
    markup += "<tr>\n";
    for(var i in columns)
        markup += getHeadCell('<a href="javascript:sortTable(rankedStatistics, ' + i + ')">' + columns[i] + '</a>');
    markup += "</tr>\n";
    for(var i in rankedStatistics)
        markup += getChampionStatisticsRow(rankedStatistics[i]);
    markup += "</table>\n";
    var container = getContainer();
    container.innerHTML = markup;
}

function sortTable(rankedStatistics, functionIndex)
{
    var container = getContainer();
    var isDescending;
    if (functionIndex == container.lastFunctionIndex)
        isDescending = !container.isDescending;
    else
        isDescending = false;
    sortingFunctions =
        [
            function (x) { return x.championName; },
            function (x) { return x.gamesPlayed; },
            function (x) { return x.wins; },
            function (x) { return x.losses; },
            function (x) { return x.winLossDifference; },
            function (x) { return x.winRatio; },
            function (x) { return x.killsPerGame; },
            function (x) { return x.deathsPerGame; },
            function (x) { return x.assistsPerGame; },
            function (x) { return x.killsAndAssistsPerDeath; },
            function (x) { return x.minionKillsPerGame; },
            function (x) { return x.goldPerGame; },
        ];
    sortingFunction = sortingFunctions[functionIndex];
    rankedStatistics.sort
    (
        function (x, y)
        {
            x = sortingFunction(x);
            y = sortingFunction(y);
            var sign = 1;
            if(isDescending)
                sign = -1;
            if(x > y)
                return sign;
            else if(x == y)
                return 0;
            else
                return - sign;
        }
    );
    container.lastFunctionIndex = functionIndex;
    container.isDescending = isDescending;
    writeTable(rankedStatistics);
}

var container = getContainer();
container.lastFunctionIndex = 0;
container.isDescending = false;