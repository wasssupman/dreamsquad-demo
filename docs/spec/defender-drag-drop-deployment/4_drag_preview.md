# Drag Preview

**작업 구분**: Phase 4

## 목적

Drag 중인 defender 의 실루엣을 pointer 아래 world 위치에 표시한다.

## 요구

- preview 는 실제 defender entity 가 아니다.
- preview 는 cost 를 차감하지 않는다.
- preview 는 tile 점유를 만들지 않는다.
- 가능하면 defender visual material 색상을 사용한다.
- Spine drag animation 이 준비된 unit 은 `dragAnimation` loop 를 사용할 수 있다.
- Spine preview 가 없으면 capsule/cube fallback 을 허용한다.

## 완료 기준

- drag 중 preview 가 pointer 를 따라간다.
- preview 는 drop/cancel 후 사라진다.
- preview 실패가 배치 로직 실패로 이어지지 않는다.
