capturador
	isUnderRepair
		priority:
			sai de construções que estão sendo capturados ou defendidos para procurar um local seguro para fundir, ou reparar.
			se não houver quem possa ocupar seu lugar quando ele sair, ele resolve ficar pra defender.
		low priority:
			antes da fase de compra, começa a fundir no caminho ou retornar para setores capturados que não estejam em estado de defesa
		
	Oportunista (unidades com plano, unidades rogue)
		desvia do seu objetivo e captura quando tiver uma oportunidade, não importa em qual plano esteja alocado, mesmo que seja rogue
		
	Defensor (predios capturados, unidades com plano)
		ocupa o predio e fica nele protegendo enquanto houverem inimigos até 2h.
		Demais unidades alocadas no plano de defesa, avaliam combates nos arredores do predio defendido.
		se forem alocadas mas estiverem longe demais, atiram em quem estiver no caminho para o setor designado
	
	Ponta de lança (unidades com plano)
		avança e captura o objetivo designado, mas no turno seguinte avança para o proximo predio para que uma unidade anterior termine a captura.
		a ponta de lança só deve sair se houver sucessor plausível ou se o setor estiver seguro o bastante, senao ele muda pra modo defensor.
		Se o setor for disputado e ele não for o responsavel pela captura daquele terreno, e houverem capturadores proximos, ele se torna "perseguidor" e apoia a captura, lutando e abrindo caminho, deixando pra ser ponta de lança qdo o setor estiver seguro.
		
	Explorador (unidades com plano, unidades rogues)
		avança no melhor DPQ para revelar quem está capturando o predio desejado que está oculto pelo FoW
		caso aja um ataque lateral oportuno proximo do local a ser revelado, ele ataca porque sabe que vai revelar qdo terminar.
		
	Perseguidor (predios não capturados, unidades com plano, unidades rogues)
		se houver capturadores em melhores condições, sai pra lutar e deixa o capturador com mais hp terminar a construção
		atira preferencialmente em quem esta no predio desejado, senao em quem esta no pior dpq e tem pouco hpq
		unidades rogues procuram quaisquer construções proximas, unidades com plano vão a seus planos
	