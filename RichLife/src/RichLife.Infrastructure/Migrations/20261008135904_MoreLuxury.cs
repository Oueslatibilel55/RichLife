using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RichLife.Infrastructure.Migrations
{
    /// <summary>
    /// 61 more luxury items (76 in total) across 16 categories — the new ones are
    /// Jewelry, Fashion, Wine, Instrument, Art, Horse, SportsTeam, Experience and Collectible
    /// (LuxuryCategory 8..16). Data only: no schema change. Photos are Wikimedia Commons files
    /// in the frontend's public/luxury/, credited per row.
    /// </summary>
    public partial class MoreLuxury : Migration
    {
        private static readonly string[] NewIds =
        [
            "omega-speedmaster", "cartier-love-bracelet", "dom-perignon", "penny-black", "leica-m3", "louis-vuitton-trunk", "harley-davidson", "hermes-birkin", "gibson-les-paul", "bmw-m4", "steinway-grand", "macallan-whisky", "royal-oak", "range-rover", "g-wagon", "riva-aquarama", "lafite-collection", "aston-martin-db11", "thoroughbred", "sailing-yacht", "diamond-necklace", "cessna-citation", "ski-chalet", "patek-nautilus", "bentley-continental", "polo-ponies", "ferrari-sf90", "mclaren-p1", "pink-diamond", "aston-valkyrie", "stradivarius", "monet-water-lilies", "bordeaux-vineyard", "beverly-hills-mansion", "manhattan-penthouse", "space-flight", "f1-car", "rembrandt", "racing-stable", "van-gogh", "ferrari-250-gto", "hope-diamond", "explorer-yacht", "airbus-acj", "basketball-franchise", "classic-car-collection", "royal-tiara", "expedition-superyacht", "monaco-villa", "orbital-trip", "caribbean-resort", "concorde", "leonardo-drawing", "private-museum", "football-club", "dubai-tower", "nfl-franchise", "moon-trip", "private-airport", "neuschwanstein", "mona-lisa",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "luxury_catalogue",
                columns: ["Id", "Name", "Category", "Description", "Price", "RequiredPrestige",
                          "ImageUrl", "ImageCredit", "ImageSourceUrl", "DisplayOrder", "IsActive"],
                values: new object[,]
            {
                { "omega-speedmaster", "Omega Speedmaster", 1, "The moonwatch. Worn on the Moon, worn to brunch.", 110000m, 2, "/luxury/omega-speedmaster.jpg", "Torsten Bolten · Public domain", "https://commons.wikimedia.org/wiki/File:OMEGA-Speedmaster-Professional-Front.jpg", 0, true },
                { "cartier-love-bracelet", "Cartier gemstone bracelet", 8, "Emeralds, rubies and sapphires — a jeweller's masterpiece.", 120000m, 2, "/luxury/cartier-love-bracelet.jpg", "Tim Evanson from Cleveland Heights, Ohio, USA · CC BY-SA 2.0", "https://commons.wikimedia.org/wiki/File:Tutti_frutti_bracelet_-_Cartier_(25802475508).jpg", 0, true },
                { "dom-perignon", "Dom Pérignon cellar", 10, "A case for every promotion — yours or the company's.", 140000m, 2, "/luxury/dom-perignon.jpg", "THOR · CC BY 2.0", "https://commons.wikimedia.org/wiki/File:DomPerignonChampagne.jpg", 0, true },
                { "penny-black", "Penny Black stamp", 16, "The world's first stamp. Never, ever lick it.", 150000m, 2, "/luxury/penny-black.jpg", "William Wyon · Public domain", "https://commons.wikimedia.org/wiki/File:%22Penny_Black%22_postage_stamps_MET_DP328201.jpg", 0, true },
                { "leica-m3", "Vintage Leica M3", 16, "German precision from 1954. Every photo looks expensive.", 160000m, 2, "/luxury/leica-m3.jpg", "Rama · CC BY-SA 2.0 fr", "https://commons.wikimedia.org/wiki/File:Leica_M3_mg_3851.jpg", 0, true },
                { "louis-vuitton-trunk", "Louis Vuitton trunk", 9, "Luggage for people who never carry their own luggage.", 180000m, 2, "/luxury/louis-vuitton-trunk.jpg", "Tim Evanson · CC BY-SA 2.0", "https://commons.wikimedia.org/wiki/File:1880_trunk_-_Louis_Vuitton.jpg", 0, true },
                { "harley-davidson", "Harley-Davidson Road Glide", 2, "Open road, loud pipes, zero quarterly reports.", 220000m, 2, "/luxury/harley-davidson.jpg", "Lightburst · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:2019_Harley-Davidson_CVO_Road_Glide_in_Red_Pepper_%26_Magnetic_Grey_With_Black_Hole.jpg", 0, true },
                { "hermes-birkin", "Hermès Birkin bag", 9, "There is a waiting list. You are not on it. Until now.", 250000m, 2, "/luxury/hermes-birkin.jpg", "Ohconfucius · CC BY 3.0", "https://commons.wikimedia.org/wiki/File:Croc_Birkin_bag.jpg", 0, true },
                { "gibson-les-paul", "1959 Gibson Les Paul", 11, "The holy grail of electric guitars. Sunburst, of course.", 400000m, 2, "/luxury/gibson-les-paul.jpg", "François laheyne · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Gibson_Les_Paul_Deluxe_Sunburst_1978.jpg", 0, true },
                { "bmw-m4", "BMW M4 Competition", 3, "Your first proper sports car. The neighbours noticed.", 450000m, 2, "/luxury/bmw-m4.jpg", "Alexander Migl · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:BMW_M4_(G82)_Competition_1X7A0305.jpg", 0, true },
                { "steinway-grand", "Steinway Model D grand piano", 11, "Concert-hall sound in your living room. Lessons not included.", 600000m, 2, "/luxury/steinway-grand.jpg", "Steinway & Sons, 10 Rondenbarg, Hamburg D-22525, Germany, eu.steinway.com.St · CC BY-SA 3.0", "https://commons.wikimedia.org/wiki/File:Steinway_%26_Sons_concert_grand_piano,_model_D-274,_manufactured_at_Steinway%27s_factory_in_Hamburg,_Germany.png", 0, true },
                { "macallan-whisky", "Rare Macallan whisky", 10, "Older than your company. Smoother than your pitch deck.", 1100000m, 3, "/luxury/macallan-whisky.jpg", "Karin Langner-Bahmann · CC BY-SA 3.0", "https://commons.wikimedia.org/wiki/File:CRW_4088_Macallan.jpg", 0, true },
                { "royal-oak", "Audemars Piguet Royal Oak", 1, "Octagonal bezel, eight screws, zero modesty.", 1200000m, 3, "/luxury/royal-oak.jpg", "Clyde94 · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Audemars_Piguet_Royal_Oak_Offshore_Diver.jpg", 0, true },
                { "range-rover", "Range Rover SV", 3, "Goes anywhere. Mostly goes to the golf club.", 1500000m, 3, "/luxury/range-rover.jpg", "Dinkun Chen · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:LAND_ROVER_RANGE_ROVER_(L460)_China.jpg", 0, true },
                { "g-wagon", "Mercedes-AMG G 63 6×6", 3, "Six wheels, portal axles, and absolutely no reason.", 1800000m, 3, "/luxury/g-wagon.jpg", "Alexander Migl · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Mercedes-Benz_W463_G_63_AMG_6%C3%976_MYLE_Festival_2025_DSC_9406.jpg", 0, true },
                { "riva-aquarama", "Riva Aquarama speedboat", 6, "Varnished mahogany and Riviera summers.", 2000000m, 3, "/luxury/riva-aquarama.jpg", "StuivertjeWisselen · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Riva_Aquarama.jpg", 0, true },
                { "lafite-collection", "Château Lafite collection", 10, "First-growth Bordeaux, a vertical of every great year.", 2200000m, 3, "/luxury/lafite-collection.jpg", "MaT-WiKi1 · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Bouteille_de_ch%C3%A2teau_Lafite_Rothschild_2002..jpg", 0, true },
                { "aston-martin-db11", "Aston Martin DB11", 3, "The grand tourer a secret agent would expense.", 3000000m, 3, "/luxury/aston-martin-db11.jpg", "Calreyn88 · CC0", "https://commons.wikimedia.org/wiki/File:Aston_Martin_DB11_6.jpg", 0, true },
                { "thoroughbred", "Thoroughbred racehorse", 13, "Bloodlines longer than most royal families.", 3500000m, 3, "/luxury/thoroughbred.jpg", "Flickr user Jeff Kubina · CC BY-SA 2.0", "https://commons.wikimedia.org/wiki/File:Horseracing_Churchill_Downs.jpg", 0, true },
                { "sailing-yacht", "Sailing yacht", 6, "Silent, elegant, and wind is free.", 4000000m, 3, "/luxury/sailing-yacht.jpg", "Liridon · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Sailing_boat_at_sunset,_Ionian_Sea,_Albania.jpg", 0, true },
                { "diamond-necklace", "Diamond necklace", 8, "Forty carats of \"I did well this year\".", 5000000m, 3, "/luxury/diamond-necklace.jpg", "Cliff from Arlington, VA (Outside Washington DC), USA · CC BY 2.0", "https://commons.wikimedia.org/wiki/File:Napoleon_Diamond_Necklace.jpg", 0, true },
                { "cessna-citation", "Cessna Citation jet", 7, "Your first private jet. Small, fast, life-changing.", 7000000m, 3, "/luxury/cessna-citation.jpg", "Alan Wilson from Peterborough, Cambs, UK · CC BY-SA 2.0", "https://commons.wikimedia.org/wiki/File:Cessna_Citation_CJ4_%E2%80%98N22UB%E2%80%99_(52910579920).jpg", 0, true },
                { "ski-chalet", "Alpine ski chalet", 4, "Ski in, ski out, fondue at nine.", 8000000m, 3, "/luxury/ski-chalet.jpg", "DimiTalen · CC0", "https://commons.wikimedia.org/wiki/File:Chalets_and_ski_lifts_near_Montfrais,_Vaujany,_2026.jpg", 0, true },
                { "patek-nautilus", "Patek Philippe Nautilus", 1, "You never really own one. You look after it for the next tycoon.", 12000000m, 4, "/luxury/patek-nautilus.jpg", "Patek Philippe SA · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Patek-Philippe-Nautilus-5711-1A-010-1.jpg", 0, true },
                { "bentley-continental", "Bentley Continental GTC", 3, "Handmade in Crewe, roof down, never in a hurry.", 14000000m, 4, "/luxury/bentley-continental.jpg", "M 93 · CC BY-SA 3.0 de", "https://commons.wikimedia.org/wiki/File:Bentley_Continental_GTC_(II)_%E2%80%93_Frontansicht_(3),_25._Oktober_2011,_D%C3%BCsseldorf.jpg", 0, true },
                { "polo-ponies", "Polo team ponies", 13, "Six ponies, one sport, and plenty of champagne.", 18000000m, 4, "/luxury/polo-ponies.jpg", "Clément Bucco-Lechat · CC BY-SA 3.0", "https://commons.wikimedia.org/wiki/File:30th_St._Moritz_Polo_World_Cup_on_Snow_-_20140202_-_Cartier_vs_Ralph_Lauren_18.jpg", 0, true },
                { "ferrari-sf90", "Ferrari SF90 Stradale", 3, "A thousand hybrid horsepower in rosso corsa.", 20000000m, 4, "/luxury/ferrari-sf90.jpg", "Calreyn88 · CC BY 4.0", "https://commons.wikimedia.org/wiki/File:Ferrari_SF90_Stradale_Mayfair.jpg", 0, true },
                { "mclaren-p1", "McLaren P1", 3, "A hypercar that redefined what fast means.", 28000000m, 4, "/luxury/mclaren-p1.jpg", "MrWalkr · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:2013_McLaren_P1_MSO.jpg", 0, true },
                { "pink-diamond", "Pair of pink diamonds", 8, "Two of the rarest stones on Earth. Earrings, maybe.", 30000000m, 4, "/luxury/pink-diamond.jpg", "roy fuchs · Public domain", "https://commons.wikimedia.org/wiki/File:Pink_Diamonds.gif", 0, true },
                { "aston-valkyrie", "Aston Martin Valkyrie", 3, "A Formula 1 car with number plates.", 35000000m, 4, "/luxury/aston-valkyrie.jpg", "Vauxford · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:2019_Aston_Martin_Valkyrie_AMR_Pro_6.5_Front.jpg", 0, true },
                { "stradivarius", "Stradivarius violin", 11, "Made in Cremona three centuries ago. Still the best.", 40000000m, 4, "/luxury/stradivarius.jpg", "Σπάρτακος · CC BY-SA 3.0", "https://commons.wikimedia.org/wiki/File:Stradivarius_violin_in_the_royal_palace_in_madrid.jpg", 0, true },
                { "monet-water-lilies", "Monet — Water Lilies", 12, "Giverny's pond, in Monet's own brushstrokes.", 45000000m, 4, "/luxury/monet-water-lilies.jpg", "Claude Monet · Public domain", "https://commons.wikimedia.org/wiki/File:Claude_Monet_-_Water_Lilies_-_Google_Art_Project.jpg", 0, true },
                { "bordeaux-vineyard", "Bordeaux wine estate", 10, "Your own château, your own label, your own vintage.", 55000000m, 4, "/luxury/bordeaux-vineyard.jpg", "PA · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Ch%C3%A2teau_Lafite-Rothschild.jpg", 0, true },
                { "beverly-hills-mansion", "Beverly Hills estate", 4, "A Tudor mansion with formal gardens and famous neighbours.", 70000000m, 4, "/luxury/beverly-hills-mansion.jpg", "Los Angeles · CC BY-SA 3.0", "https://commons.wikimedia.org/wiki/File:Greystone_Mansion.JPG", 0, true },
                { "manhattan-penthouse", "Manhattan penthouse", 4, "Central Park view from the 90th floor.", 120000000m, 5, "/luxury/manhattan-penthouse.jpg", "Percival Kestreltail · CC BY-SA 3.0", "https://commons.wikimedia.org/wiki/File:Central_Park_Tower_April_2021.jpg", 0, true },
                { "space-flight", "Suborbital space flight", 15, "Eleven minutes, four of them weightless. Worth every dollar.", 150000000m, 5, "/luxury/space-flight.jpg", "Lauren Harnett · Public domain", "https://commons.wikimedia.org/wiki/File:Blue_Origin_test_fires_a_powerful_new_hydrogen-_and_oxygen-fueled.jpg", 0, true },
                { "f1-car", "Formula 1 car", 3, "A championship-winning chassis for your private track days.", 160000000m, 5, "/luxury/f1-car.jpg", "Xavigivax · CC BY-SA 3.0", "https://commons.wikimedia.org/wiki/File:070616_Ferrari_F1_2007_01.JPG", 0, true },
                { "rembrandt", "Rembrandt self-portrait", 12, "The Dutch master looking back at you over the fireplace.", 180000000m, 5, "/luxury/rembrandt.jpg", "Rembrandt · Public domain", "https://commons.wikimedia.org/wiki/File:Rembrandt_self_portrait.jpg", 0, true },
                { "racing-stable", "Racing stable", 13, "Thirty horses, a trainer and a box at Ascot.", 220000000m, 5, "/luxury/racing-stable.jpg", "Richard Humphrey · CC BY-SA 2.0", "https://commons.wikimedia.org/wiki/File:Warren_Place_horse_racing_stables,_Newmarket_-_geograph.org.uk_-_6134218.jpg", 0, true },
                { "van-gogh", "Van Gogh — Wheat Field with Cypresses", 12, "Swirling Provençal sky, unmistakably Vincent.", 250000000m, 5, "/luxury/van-gogh.jpg", "Vincent van Gogh · Public domain", "https://commons.wikimedia.org/wiki/File:Vincent_van_Gogh_-_Wheat_Field_with_Cypresses_(National_Gallery_version).jpg", 0, true },
                { "ferrari-250-gto", "Ferrari 250 GTO", 3, "The most valuable car ever sold. Only 36 were made.", 300000000m, 5, "/luxury/ferrari-250-gto.jpg", "Unknown · CC BY-SA 3.0", "https://commons.wikimedia.org/wiki/File:1962_Ferrari_250_GTO_34_2.jpg", 0, true },
                { "hope-diamond", "Legendary blue diamond", 8, "45 carats of deep blue with a famously cursed history.", 350000000m, 5, "/luxury/hope-diamond.jpg", "Unknown authorUnknown author · Public domain", "https://commons.wikimedia.org/wiki/File:The_Hope_Diamond_-_SIA.jpg", 0, true },
                { "explorer-yacht", "Explorer yacht", 6, "Ice-class hull for Antarctica, a submarine in the garage.", 500000000m, 5, "/luxury/explorer-yacht.jpg", "Gillfoto · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Sherpa_815_Explorer_6874.jpg", 0, true },
                { "airbus-acj", "Airbus ACJ320", 7, "An airliner with a bedroom, an office and a shower.", 700000000m, 5, "/luxury/airbus-acj.jpg", "Comlux Aviation Group · CC BY-SA 3.0", "https://commons.wikimedia.org/wiki/File:Comlux_Airbus_ACJ320_VVIP_interior.jpg", 0, true },
                { "basketball-franchise", "Basketball franchise", 14, "Courtside seats? You own the court.", 800000000m, 5, "/luxury/basketball-franchise.jpg", "pxhere.com · CC0", "https://commons.wikimedia.org/wiki/File:Basketball_arena_match_sport_game_lakers_tribune_fans-1223638.jpg", 0, true },
                { "classic-car-collection", "Classic car collection", 16, "Fifty legends of motoring in a climate-controlled hall.", 1200000000m, 6, "/luxury/classic-car-collection.jpg", "Dwxn · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Malta_Classic_Car_Museum_20231205_162720.jpg", 0, true },
                { "royal-tiara", "Royal diamond tiara", 8, "Once worn by an empress. Now worn at your gala.", 1500000000m, 6, "/luxury/royal-tiara.jpg", "Ed Uthman from Houston, TX, USA · CC BY 2.0", "https://commons.wikimedia.org/wiki/File:Emerald_%26_Diamond_Tiara_(4739771202).jpg", 0, true },
                { "expedition-superyacht", "Expedition superyacht", 6, "Helipad, submarine and a crew of sixty.", 1800000000m, 6, "/luxury/expedition-superyacht.jpg", "Myrabella · CC BY-SA 3.0", "https://commons.wikimedia.org/wiki/File:Lady_Lau_super-yacht_IMO_1010674_Bonifacio.jpg", 0, true },
                { "monaco-villa", "Monaco villa", 4, "Overlooking the harbour and the Grand Prix.", 2000000000m, 6, "/luxury/monaco-villa.jpg", "Florent Abel · CC BY-SA 4.0", "https://commons.wikimedia.org/wiki/File:Villa_Ispahan,_Monaco.jpg", 0, true },
                { "orbital-trip", "Orbital space trip", 15, "Three days in orbit, sixteen sunrises a day.", 2500000000m, 6, "/luxury/orbital-trip.jpg", "NASA/SpaceX · Public domain", "https://commons.wikimedia.org/wiki/File:SpaceX_Crew_Dragon_(More_cropped).jpg", 0, true },
                { "caribbean-resort", "Caribbean resort", 4, "Your own five-star resort. You always get a room.", 3000000000m, 6, "/luxury/caribbean-resort.jpg", "Michael Gray from Wantagh NY, USA · CC BY-SA 2.0", "https://commons.wikimedia.org/wiki/File:Caribbean_Beach_Resort_pool_by_mrkathika.jpg", 0, true },
                { "concorde", "Concorde", 7, "Supersonic, retired, and now entirely yours.", 3500000000m, 6, "/luxury/concorde.jpg", "Eduard Marmet · CC BY-SA 3.0", "https://commons.wikimedia.org/wiki/File:British_Airways_Concorde_G-BOAC_03.jpg", 0, true },
                { "leonardo-drawing", "Leonardo da Vinci — Vitruvian Man", 12, "The most famous drawing ever made, by the original genius.", 4000000000m, 6, "/luxury/leonardo-drawing.jpg", "Leonardo da Vinci · Public domain", "https://commons.wikimedia.org/wiki/File:Da_Vinci_Vitruve_Luc_Viatour.jpg", 0, true },
                { "private-museum", "Private art museum", 12, "Your name over the door, your collection on the walls.", 5000000000m, 6, "/luxury/private-museum.jpg", "Michael D Beckwith · CC BY 3.0", "https://commons.wikimedia.org/wiki/File:Kelvingrove_Art_Gallery_and_Museum_Central_Hall.jpg", 0, true },
                { "football-club", "European football club", 14, "Sixty thousand fans singing for your team.", 6000000000m, 6, "/luxury/football-club.jpg", "Oh-Barcelona.com from Barcelona, Spain · CC BY 2.0", "https://commons.wikimedia.org/wiki/File:Camp_Nou_aerial_(cropped).jpg", 0, true },
                { "dubai-tower", "Dubai skyscraper", 4, "Eighty floors of glass above the Gulf.", 8000000000m, 6, "/luxury/dubai-tower.jpg", "Norlando Pobre · CC BY 2.0", "https://commons.wikimedia.org/wiki/File:Dubai_Marina_Skyline.jpg", 0, true },
                { "nfl-franchise", "American football franchise", 14, "A stadium of seventy thousand, every Sunday.", 12000000000m, 7, "/luxury/nfl-franchise.jpg", "Thank You (21 Millions+) views · CC BY 2.0", "https://commons.wikimedia.org/wiki/File:SoFi_Stadium_(51126606022).jpg", 0, true },
                { "moon-trip", "Trip around the Moon", 15, "See the far side with your own eyes. Earthrise included.", 15000000000m, 7, "/luxury/moon-trip.jpg", "NASA / Goddard Space Flight Center / Arizona State University · Public domain", "https://commons.wikimedia.org/wiki/File:Earthrise_over_Compton_crater_-LRO_full_res.jpg", 0, true },
                { "private-airport", "Private airport", 7, "Your own runway, your own tower, your own rules.", 20000000000m, 7, "/luxury/private-airport.jpg", "BulbazaurREX · CC0", "https://commons.wikimedia.org/wiki/File:WSI_airport_runway_2025.jpg", 0, true },
                { "neuschwanstein", "Fairy-tale castle", 4, "The castle that inspired the fairy tales.", 30000000000m, 7, "/luxury/neuschwanstein.jpg", "Wilfredor · CC0", "https://commons.wikimedia.org/wiki/File:Neuschwanstein_Castle_2024-02.jpg", 0, true },
                { "mona-lisa", "Mona Lisa", 12, "The most famous painting in the world. Priceless — until now.", 80000000000m, 7, "/luxury/mona-lisa.jpg", "Leonardo da Vinci · Public domain", "https://commons.wikimedia.org/wiki/File:Mona_Lisa_headcrop.jpg", 0, true },
            });

            // One display order for the whole collection: by prestige, then price.
            migrationBuilder.Sql("""
                UPDATE luxury_catalogue l
                SET "DisplayOrder" = r.n * 10
                FROM (SELECT "Id", row_number() OVER (ORDER BY "RequiredPrestige", "Price") AS n
                      FROM luxury_catalogue) r
                WHERE l."Id" = r."Id";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var id in NewIds)
                migrationBuilder.DeleteData(table: "luxury_catalogue", keyColumn: "Id", keyValue: id);
        }
    }
}
