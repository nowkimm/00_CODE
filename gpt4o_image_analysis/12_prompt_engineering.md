# GPT-4o 이미지 생성 프롬프트 엔지니어링 가이드

## 1. 개요

이 문서는 GPT-4o 이미지 생성 기능을 최대한 활용하기 위한 프롬프트 작성 기법과 전략을 제공한다. 효과적인 프롬프트 작성은 원하는 결과물을 얻는 핵심이다.

## 2. 프롬프트 구조 기본

### 2.1 핵심 구성 요소

```
효과적인 프롬프트의 5가지 구성 요소:

1. 주제 (Subject) - 무엇을?
2. 스타일 (Style) - 어떤 방식으로?
3. 구도 (Composition) - 어떻게 배치?
4. 분위기 (Mood) - 어떤 느낌으로?
5. 기술적 세부사항 (Technical) - 어떤 품질로?
```

### 2.2 기본 프롬프트 공식

```
[형식] of [주제] in [스타일],
featuring [주요 요소들],
set in [환경/배경],
with [조명 조건],
[분위기/감정],
[기술적 세부사항]
```

**예시:**
```
Professional photograph of a golden retriever puppy 
in portrait style,
featuring expressive eyes and soft fur texture,
set in a sunny garden with blooming flowers,
with natural golden hour lighting,
warm and joyful mood,
high resolution, shallow depth of field
```

## 3. 상세 요소별 가이드

### 3.1 주제 (Subject) 명시

**좋은 예:**
```
✓ "A 35-year-old Korean businessman"
✓ "A sleek electric sports car"
✓ "A Victorian-era mansion"
✓ "A steaming cup of matcha latte"
```

**나쁜 예:**
```
✗ "A person" (너무 모호)
✗ "A nice car" (구체성 부족)
✗ "A building" (특징 없음)
```

### 3.2 스타일 지정

**아트 스타일:**
```
- Photorealistic / Hyperrealistic
- Oil painting / Watercolor
- Digital art / Vector illustration
- Anime / Manga / Webtoon
- Pixel art (8-bit / 16-bit / 32-bit)
- Line art / Sketch
- Minimalist / Maximalist
```

**사진 스타일:**
```
- Portrait photography
- Landscape photography
- Macro photography
- Product photography
- Street photography
- Fashion editorial
- Documentary style
```

**시대별 스타일:**
```
- Renaissance (르네상스)
- Baroque (바로크)
- Art Nouveau (아르누보)
- Art Deco (아르데코)
- Mid-century modern
- Contemporary
- Futuristic
```

### 3.3 조명 지정

**자연광:**
```
- Golden hour (황금빛 시간대)
- Blue hour (블루아워)
- Overcast soft light (흐린 날 부드러운 빛)
- Harsh midday sun (한낮의 강한 햇살)
- Dappled sunlight through trees (나뭇잎 사이 햇살)
- Backlit / silhouette (역광)
```

**인공 조명:**
```
- Studio lighting (스튜디오 조명)
- Rembrandt lighting (렘브란트 조명)
- Ring light (링 라이트)
- Neon lights (네온 조명)
- Candlelight (촛불)
- Dramatic spotlight (극적인 스포트라이트)
```

### 3.4 구도 및 앵글

**카메라 앵글:**
```
- Eye level (눈높이)
- Low angle / worm's eye view (저각)
- High angle / bird's eye view (고각)
- Dutch angle / tilted (틸트)
- Overhead / top-down (위에서)
- Close-up (클로즈업)
- Wide shot / establishing shot (와이드샷)
```

**구도 기법:**
```
- Rule of thirds (삼분할법)
- Centered / symmetrical (중앙/대칭)
- Leading lines (유도선)
- Frame within frame (프레임 속 프레임)
- Negative space (여백)
- Golden ratio (황금비율)
```

### 3.5 색상 지정

**색상 팔레트:**
```
- Warm palette (따뜻한 색상)
- Cool palette (차가운 색상)
- Monochromatic (단색조)
- Complementary colors (보색)
- Analogous colors (유사색)
- Pastel colors (파스텔)
- Vibrant/Saturated (비비드)
- Muted/Desaturated (저채도)
```

**구체적 색상:**
```
- Hex 코드: #FF5733, #3498DB
- 이름: "cobalt blue", "burnt sienna"
- 참조: "Pantone 17-5104 Ultimate Gray"
```

## 4. 고급 프롬프트 기법

### 4.1 네거티브 프롬프트 활용

**원치 않는 요소 제외:**
```
"Professional headshot portrait,
clean background,
natural lighting,
no watermarks,
no text,
no logos,
no distortions,
avoid cartoonish features"
```

### 4.2 참조 스타일 지정

**아티스트/작품 참조:**
```
"In the style of..."
- "...Studio Ghibli animation"
- "...Blade Runner 2049 cinematography"
- "...Wes Anderson color palette"
- "...Norman Rockwell illustration"
```

**주의:** 생존 작가의 스타일은 제한될 수 있음

### 4.3 멀티턴 대화 활용

**반복적 개선 전략:**
```
턴 1: 기본 이미지 생성
"Create a fantasy castle on a mountain"

턴 2: 스타일 조정
"Make it more Gothic architecture style"

턴 3: 분위기 변경
"Add dramatic storm clouds and lightning"

턴 4: 디테일 추가
"Include a winding path leading up to the castle"

턴 5: 최종 조정
"Brighten the castle windows with warm light"
```

### 4.4 캐릭터 일관성 유지

**캐릭터 DNA 템플릿:**
```
CHARACTER PROFILE:
Name: [캐릭터명]
Age: [나이]
Gender: [성별]

Physical Features:
- Face shape: [얼굴형]
- Eyes: [눈 색상, 모양, 특징]
- Hair: [헤어 스타일, 색상, 길이]
- Skin: [피부 톤]
- Build: [체형]
- Height: [키]

Distinguishing features:
- [특징 1]
- [특징 2]

Clothing style:
- [의상 설명]

---
Now show this character [새로운 상황/포즈]
```

### 4.5 복잡한 장면 분해

**단계적 접근:**
```
복잡한 장면을 요소별로 분해:

1. 배경 레이어:
   "Medieval marketplace background with stone buildings"

2. 중경 요소:
   "Wooden market stalls with colorful canopies"

3. 전경 캐릭터:
   "Merchant selling fruits, customer examining goods"

4. 분위기 요소:
   "Morning mist, warm sunlight, busy atmosphere"

5. 디테일:
   "Cobblestone street, hanging signs, pigeons"
```

## 5. 용도별 프롬프트 템플릿

### 5.1 제품 사진

```
Professional product photography of [제품]:
- Shot type: [Hero shot / Detail / Lifestyle]
- Background: [배경 색상/재질]
- Surface: [표면 재질]
- Lighting: [조명 설정]
- Angle: [촬영 각도]
- Props: [소품] (optional)
- Style: E-commerce ready, high-end quality
- Reflection and shadows: [있음/없음]
```

### 5.2 소셜미디어 콘텐츠

```
Social media [플랫폼] post image:
- Content type: [피드/스토리/릴스/썸네일]
- Topic: [주제]
- Visual hook: [시선 끄는 요소]
- Text space: [텍스트 배치 위치]
- Brand colors: [브랜드 색상]
- Aspect ratio: [비율]
- Mood: [분위기]
- Target audience: [타겟]
```

### 5.3 교육 자료

```
Educational illustration explaining [주제]:
- Target age: [대상 연령]
- Complexity level: [복잡도]
- Style: [스타일 - 다이어그램/인포그래픽/일러스트]
- Labels: [레이블 포함 여부]
- Color coding: [색상 코딩 방식]
- Layout: [레이아웃]
- Text integration: [텍스트 통합 방식]
```

### 5.4 게임 아트

```
Game art for [게임 장르]:
- Asset type: [캐릭터/환경/아이템/UI]
- Art style: [아트 스타일]
- Perspective: [시점]
- Animation ready: [애니메이션용 여부]
- Mood: [분위기]
- Color palette: [색상 팔레트]
- Technical: [해상도/투명 배경 등]
```

### 5.5 마케팅/광고

```
Marketing visual for [브랜드/제품]:
- Campaign goal: [캠페인 목표]
- Target audience: [타겟 오디언스]
- Key message space: [메시지 영역]
- Brand guidelines: [브랜드 가이드라인]
- Emotion to evoke: [유발할 감정]
- Call-to-action area: [CTA 영역]
- Platform: [사용 플랫폼]
```

## 6. 문제 해결 가이드

### 6.1 일반적인 문제와 해결책

**문제: 텍스트가 정확하지 않음**
```
해결책:
- 텍스트를 따옴표로 감싸기: "SALE"
- 간단하고 짧은 텍스트 사용
- 폰트 스타일 명시
- 별도 편집 툴 사용 고려
```

**문제: 일관성 없는 캐릭터**
```
해결책:
- 상세한 캐릭터 프로필 사용
- 참조 이미지 업로드
- 동일한 기본 프롬프트 유지
- "Exact same character" 명시
```

**문제: 원치 않는 요소 포함**
```
해결책:
- 네거티브 프롬프트 추가
- "without", "no", "avoid" 사용
- 더 구체적인 설명 제공
- 재생성 요청
```

**문제: 스타일이 일치하지 않음**
```
해결책:
- 스타일 참조를 더 구체적으로
- 예시 작품/아티스트 언급
- 색상 팔레트 명시
- 동일 세션에서 작업
```

### 6.2 품질 향상 팁

```
1. 구체적일수록 좋음
   "dog" → "golden retriever puppy, 3 months old"

2. 품질 키워드 추가
   "high resolution", "detailed", "professional"

3. 조명 명시
   "soft natural lighting" 등 구체적 조명

4. 참조 활용
   "like a magazine cover" 등 참조점 제공

5. 반복 개선
   생성 후 피드백으로 개선
```

## 7. 프롬프트 작성 체크리스트

```
□ 주제가 명확하게 정의되었는가?
□ 스타일이 구체적으로 지정되었는가?
□ 구도/앵글이 명시되었는가?
□ 조명 조건이 설명되었는가?
□ 색상/팔레트가 지정되었는가?
□ 분위기/감정이 전달되었는가?
□ 기술적 요구사항이 포함되었는가?
□ 원치 않는 요소가 제외되었는가?
□ 용도에 맞는 형식인가?
□ 프롬프트가 너무 길지 않은가?
```

## 8. 효과적인 키워드 사전

### 8.1 품질 관련

```
높은 품질: high quality, detailed, sharp, crisp, HD, 4K, 8K
전문적: professional, studio quality, magazine quality
사실적: photorealistic, hyperrealistic, lifelike
예술적: artistic, creative, expressive, unique
```

### 8.2 분위기 관련

```
밝음: bright, cheerful, vibrant, lively, energetic
어두움: dark, moody, mysterious, dramatic, noir
평화: peaceful, serene, calm, tranquil, zen
강렬: intense, powerful, dynamic, bold, striking
```

### 8.3 구도 관련

```
가까움: close-up, macro, detail shot, tight crop
멀음: wide shot, panoramic, establishing shot
각도: low angle, high angle, aerial view, eye level
초점: shallow DOF, bokeh, focused, sharp throughout
```

---

## 참고 출처
- OpenAI 공식 프롬프트 가이드
- Midjourney 프롬프트 기법
- 디지털 아트 커뮤니티 베스트 프랙티스
- 사진 구도/조명 이론
- 사용자 피드백 분석
